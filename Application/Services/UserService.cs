using DoIt.Api.Application.Interfaces;
using DoIt.Api.Application.Mapping;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class UserService(DoItDbContext dbContext, IPasswordHasher passwordHasher) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.OrderBy(user => user.Username).ToListAsync(cancellationToken);
        return users.Select(user => user.ToResponse()).ToList();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        if (await dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "user_exists", "Username already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            PreferredLocale = string.IsNullOrWhiteSpace(request.Locale) ? "es" : request.Locale.Trim(),
            Role = ParseEnum(request.Role, UserRole.User),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user.ToResponse();
    }

    public async Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.PreferredLocale = string.IsNullOrWhiteSpace(request.Locale) ? user.PreferredLocale : request.Locale.Trim();
        user.Role = ParseEnum(request.Role, user.Role);
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return user.ToResponse();
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(candidate => candidate.RefreshTokens)
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        user.PasswordHash = passwordHasher.HashPassword(request.Password);
        user.UpdatedAt = DateTime.UtcNow;
        foreach (var refreshToken in user.RefreshTokens.Where(token => token.RevokedAt is null))
        {
            refreshToken.RevokedAt = user.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "username_required", "Username is required.");
        }

        return username.Trim().ToLowerInvariant();
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<TEnum>(normalized, true, out var parsed) ? parsed : fallback;
    }
}
