using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Configuration;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using DoIt.Api.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Application.Services;

public sealed partial class HouseholdConnectionService(
    DoItDbContext dbContext,
    HouseholdIntegrationTokenCodec tokenCodec,
    IOptions<HouseholdIntegrationSettings> options) : IHouseholdConnectionService
{
    private readonly HouseholdIntegrationSettings _settings = options.Value;

    public async Task<HouseholdAuthorizeResponse> AuthorizeAsync(
        Guid userId,
        HouseholdAuthorizeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateClientAndRedirect(request.ClientId, request.RedirectUri);
        ValidateState(request.State);
        ValidatePkceChallenge(request.CodeChallenge, request.CodeChallengeMethod);
        var scopes = ValidateScopes(request.Scopes);

        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.IsActive,
            cancellationToken);
        if (user is null)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "user_unavailable", "The source account is unavailable.");
        }

        if (request.Approved is false)
        {
            return new HouseholdAuthorizeResponse(BuildRedirect(request.RedirectUri, request.State, error: "access_denied"));
        }

        var now = DateTime.UtcNow;
        var rawCode = HouseholdIntegrationTokenCodec.CreateCredential(32);
        var connection = new HouseholdConnection
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ClientId = _settings.ClientId,
            GrantedScopes = string.Join(' ', scopes),
            Status = HouseholdConnection.ActiveStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
        var authorizationCode = new HouseholdAuthorizationCode
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            CodeHash = HouseholdIntegrationTokenCodec.HashCredential(rawCode),
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(AuthorizationCodeMinutes)
        };

        dbContext.HouseholdConnections.Add(connection);
        dbContext.HouseholdAuthorizationCodes.Add(authorizationCode);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new HouseholdAuthorizeResponse(BuildRedirect(request.RedirectUri, request.State, code: rawCode));
    }

    public Task<HouseholdTokenResponse> ExchangeAsync(HouseholdTokenRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ClientId, _settings.ClientId, StringComparison.Ordinal))
        {
            throw InvalidGrant("invalid_client", "Unknown integration client.");
        }

        return request.GrantType switch
        {
            "authorization_code" => ExchangeAuthorizationCodeAsync(request, cancellationToken),
            "refresh_token" => RefreshAsync(request, cancellationToken),
            _ => throw InvalidGrant("unsupported_grant_type", "Unsupported grant type.")
        };
    }

    public async Task RevokeAsync(HouseholdRevokeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 8192)
        {
            return;
        }

        var tokenHash = HouseholdIntegrationTokenCodec.HashCredential(request.Token);
        Guid? connectionId = null;
        if (string.Equals(request.TokenTypeHint, "refresh_token", StringComparison.Ordinal))
        {
            connectionId = await dbContext.HouseholdRefreshTokens
                .Where(token => token.TokenHash == tokenHash)
                .Select(token => (Guid?)token.ConnectionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        connectionId ??= await dbContext.HouseholdAccessTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.ConnectionId)
            .FirstOrDefaultAsync(cancellationToken);
        connectionId ??= await dbContext.HouseholdRefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.ConnectionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (connectionId is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = await dbContext.HouseholdConnections.FirstOrDefaultAsync(
            candidate => candidate.Id == connectionId.Value,
            cancellationToken);
        if (connection is not null)
        {
            connection.Status = HouseholdConnection.RevokedStatus;
            connection.RevokedAt ??= now;
            connection.UpdatedAt = now;
            await RevokeConnectionTokensAsync(connection.Id, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HouseholdMeResponse> GetMeAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.HouseholdConnections
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken);
        if (connection is null ||
            connection.Status != HouseholdConnection.ActiveStatus ||
            connection.RevokedAt is not null ||
            connection.User is null ||
            !connection.User.IsActive)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_integration_token", "Invalid integration access token.");
        }

        return new HouseholdMeResponse(
            connection.Id,
            Account(connection.User),
            HouseholdIntegrationTokenCodec.ParseScopes(connection.GrantedScopes));
    }

    private async Task<HouseholdTokenResponse> ExchangeAuthorizationCodeAsync(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 512 ||
            string.IsNullOrWhiteSpace(request.RedirectUri) ||
            string.IsNullOrWhiteSpace(request.CodeVerifier))
        {
            throw InvalidGrant();
        }

        ValidateRegisteredRedirect(request.RedirectUri);
        var codeHash = HouseholdIntegrationTokenCodec.HashCredential(request.Code);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var storedCode = await dbContext.HouseholdAuthorizationCodes
            .Include(code => code.Connection)
            .ThenInclude(connection => connection!.User)
            .FirstOrDefaultAsync(code => code.CodeHash == codeHash, cancellationToken);
        var now = DateTime.UtcNow;
        if (storedCode is null ||
            storedCode.ConsumedAt is not null ||
            storedCode.ExpiresAt <= now ||
            !string.Equals(storedCode.RedirectUri, request.RedirectUri, StringComparison.Ordinal) ||
            storedCode.Connection is null ||
            storedCode.Connection.Status != HouseholdConnection.ActiveStatus ||
            storedCode.Connection.RevokedAt is not null ||
            storedCode.Connection.User is null ||
            !storedCode.Connection.User.IsActive)
        {
            throw InvalidGrant();
        }

        if (!VerifyPkce(request.CodeVerifier, storedCode.CodeChallenge))
        {
            storedCode.ConsumedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidGrant();
        }

        var consumed = await dbContext.HouseholdAuthorizationCodes
            .Where(code => code.Id == storedCode.Id && code.ConsumedAt == null && code.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(code => code.ConsumedAt, now), cancellationToken);
        if (consumed != 1)
        {
            throw InvalidGrant();
        }

        var response = IssueTokenPair(storedCode.Connection, Guid.NewGuid(), now);
        storedCode.Connection.LastUsedAt = now;
        storedCode.Connection.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private async Task<HouseholdTokenResponse> RefreshAsync(HouseholdTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 1024)
        {
            throw InvalidGrant();
        }

        var tokenHash = HouseholdIntegrationTokenCodec.HashCredential(request.RefreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var storedToken = await dbContext.HouseholdRefreshTokens
            .AsNoTracking()
            .Include(token => token.Connection)
            .ThenInclude(connection => connection!.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        var now = DateTime.UtcNow;
        if (storedToken is null)
        {
            throw InvalidGrant();
        }

        if (storedToken.RevokedAt is not null || storedToken.ReplacedByTokenId is not null)
        {
            await RevokeFamilyAsync(storedToken.ConnectionId, storedToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidGrant("refresh_token_reuse", "Refresh token reuse detected; this token family was revoked.");
        }

        if (storedToken.ExpiresAt <= now ||
            storedToken.Connection is null ||
            storedToken.Connection.Status != HouseholdConnection.ActiveStatus ||
            storedToken.Connection.RevokedAt is not null ||
            storedToken.Connection.User is null ||
            !storedToken.Connection.User.IsActive)
        {
            throw InvalidGrant();
        }

        var replacementId = Guid.NewGuid();
        var rotated = await dbContext.HouseholdRefreshTokens
            .Where(token => token.Id == storedToken.Id && token.RevokedAt == null && token.ReplacedByTokenId == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.ReplacedByTokenId, replacementId), cancellationToken);
        if (rotated != 1)
        {
            await RevokeFamilyAsync(storedToken.ConnectionId, storedToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidGrant("refresh_token_reuse", "Refresh token reuse detected; this token family was revoked.");
        }

        var response = IssueTokenPair(storedToken.Connection, storedToken.FamilyId, now, replacementId);
        await dbContext.HouseholdConnections
            .Where(connection => connection.Id == storedToken.ConnectionId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(connection => connection.LastUsedAt, now)
                .SetProperty(connection => connection.UpdatedAt, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private HouseholdTokenResponse IssueTokenPair(
        HouseholdConnection connection,
        Guid familyId,
        DateTime now,
        Guid? refreshTokenId = null)
    {
        var rawRefreshToken = HouseholdIntegrationTokenCodec.CreateCredential(64);
        var refreshExpiresAt = now.AddDays(RefreshTokenDays);
        dbContext.HouseholdRefreshTokens.Add(new HouseholdRefreshToken
        {
            Id = refreshTokenId ?? Guid.NewGuid(),
            ConnectionId = connection.Id,
            TokenHash = HouseholdIntegrationTokenCodec.HashCredential(rawRefreshToken),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });

        var accessExpiresAt = now.AddMinutes(AccessTokenMinutes);
        var (rawAccessToken, storedAccessToken) = tokenCodec.CreateAccessToken(connection, familyId, now, accessExpiresAt);
        dbContext.HouseholdAccessTokens.Add(storedAccessToken);

        return new HouseholdTokenResponse(
            "Bearer",
            rawAccessToken,
            checked(AccessTokenMinutes * 60),
            rawRefreshToken,
            checked(RefreshTokenDays * 24 * 60 * 60),
            connection.GrantedScopes,
            connection.Id,
            Account(connection.User!));
    }

    private async Task RevokeFamilyAsync(Guid connectionId, Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        await dbContext.HouseholdRefreshTokens
            .Where(token => token.ConnectionId == connectionId && token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await dbContext.HouseholdAccessTokens
            .Where(token => token.ConnectionId == connectionId && token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private async Task RevokeConnectionTokensAsync(Guid connectionId, DateTime now, CancellationToken cancellationToken)
    {
        await dbContext.HouseholdRefreshTokens
            .Where(token => token.ConnectionId == connectionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await dbContext.HouseholdAccessTokens
            .Where(token => token.ConnectionId == connectionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private void ValidateClientAndRedirect(string clientId, string redirectUri)
    {
        if (!string.Equals(clientId, _settings.ClientId, StringComparison.Ordinal))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_client", "Unknown integration client.");
        }

        ValidateRegisteredRedirect(redirectUri);
    }

    private void ValidateRegisteredRedirect(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri) || !_settings.ParsedRedirectUris.Contains(redirectUri))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_redirect_uri", "The redirect URI is not registered.");
        }
    }

    private static void ValidateState(string state)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length is < 16 or > 512)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_request", "A valid state value is required.");
        }
    }

    private static void ValidatePkceChallenge(string challenge, string method)
    {
        if (!string.Equals(method, "S256", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(challenge) ||
            challenge.Length != 43 ||
            !Base64UrlPattern().IsMatch(challenge))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_request", "PKCE S256 is required.");
        }
    }

    private static IReadOnlyCollection<string> ValidateScopes(IReadOnlyCollection<string>? requestedScopes)
    {
        if (requestedScopes is null || requestedScopes.Count == 0 ||
            requestedScopes.Any(scope => !HouseholdIntegrationScopes.Allowed.Contains(scope)))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_scope", "One or more requested scopes are not available.");
        }

        return requestedScopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool VerifyPkce(string verifier, string expectedChallenge)
    {
        if (verifier.Length is < 43 or > 128 || !PkceVerifierPattern().IsMatch(verifier))
        {
            return false;
        }

        var actualChallenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualChallenge),
            Encoding.ASCII.GetBytes(expectedChallenge));
    }

    private HouseholdIntegrationAccountResponse Account(User user) =>
        new(tokenCodec.CreateAccountId(user.Id), user.DisplayName);

    private static string BuildRedirect(string redirectUri, string state, string? code = null, string? error = null)
    {
        var values = new Dictionary<string, string?> { ["state"] = state };
        if (code is not null)
        {
            values["code"] = code;
        }
        if (error is not null)
        {
            values["error"] = error;
        }

        return QueryHelpers.AddQueryString(redirectUri, values);
    }

    private static ApiException InvalidGrant(string code = "invalid_grant", string message = "Invalid or expired integration grant.") =>
        new(StatusCodes.Status400BadRequest, code, message);

    private int AccessTokenMinutes => Math.Clamp(_settings.AccessTokenMinutes, 1, 60);
    private int RefreshTokenDays => Math.Clamp(_settings.RefreshTokenDays, 1, 365);
    private int AuthorizationCodeMinutes => Math.Clamp(_settings.AuthorizationCodeMinutes, 1, 10);

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)]
    private static partial Regex Base64UrlPattern();

    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex PkceVerifierPattern();
}
