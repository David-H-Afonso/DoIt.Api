using DoIt.Api.Application.Interfaces;
using DoIt.Api.Application.Mapping;
using DoIt.Api.Common;
using DoIt.Api.Configuration;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Application.Services;

public sealed class AuthService(
    DoItDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        if (await dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "user_exists", "Username already exists.");
        }

        var isFirstUser = !await dbContext.Users.AnyAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            PreferredLocale = string.IsNullOrWhiteSpace(request.Locale) ? "es" : request.Locale.Trim(),
            Role = isFirstUser ? UserRole.Admin : UserRole.User,
            CreatedAt = now,
            UpdatedAt = now,
            LastLoginAt = now
        };

        dbContext.Users.Add(user);
        var response = CreateAuthResponse(user, ipAddress, now).Response;
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_credentials", "Invalid username or password.");
        }

        var now = DateTime.UtcNow;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var response = CreateAuthResponse(user, ipAddress, now).Response;
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.User is null || !storedToken.IsActive)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_refresh_token", "Invalid refresh token.");
        }

        if (!storedToken.User.IsActive)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "inactive_user", "User is inactive.");
        }

        var now = DateTime.UtcNow;
        var (response, replacementTokenId) = CreateAuthResponse(storedToken.User, ipAddress, now);
        storedToken.RevokedAt = now;
        storedToken.ReplacedByTokenId = replacementTokenId;
        storedToken.User.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null)
        {
            return;
        }

        storedToken.RevokedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        return user.ToResponse();
    }

    private (AuthResponse Response, Guid RefreshTokenId) CreateAuthResponse(User user, string? ipAddress, DateTime now)
    {
        var accessTokenExpiresAt = now.AddMinutes(_jwtSettings.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(_jwtSettings.RefreshTokenDays);
        var refreshToken = jwtTokenService.CreateRefreshToken();
        var storedRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = jwtTokenService.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = refreshTokenExpiresAt,
            CreatedByIp = ipAddress
        };

        dbContext.RefreshTokens.Add(storedRefreshToken);

        return (new AuthResponse(
            user.ToResponse(),
            jwtTokenService.CreateAccessToken(user, accessTokenExpiresAt),
            refreshToken,
            accessTokenExpiresAt,
            refreshTokenExpiresAt), storedRefreshToken.Id);
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "username_required", "Username is required.");
        }

        return username.Trim().ToLowerInvariant();
    }
}
