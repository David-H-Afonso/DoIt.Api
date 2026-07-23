using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Security;

public sealed class HouseholdIntegrationAuthenticationEvents(DoItDbContext dbContext) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var connectionIdValue = context.Principal?.FindFirstValue("connection_id");
        var jwtId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var tokenUse = context.Principal?.FindFirstValue("token_use");
        var rawToken = ReadBearerToken(context.HttpContext.Request);
        if (!Guid.TryParse(connectionIdValue, out var connectionId) ||
            string.IsNullOrWhiteSpace(jwtId) ||
            !string.Equals(tokenUse, "household_integration", StringComparison.Ordinal) ||
            rawToken is null)
        {
            context.Fail("Invalid integration access token.");
            return;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HouseholdIntegrationTokenCodec.HashCredential(rawToken);
        var storedToken = await dbContext.HouseholdAccessTokens
            .Include(token => token.Connection)
            .ThenInclude(connection => connection!.User)
            .FirstOrDefaultAsync(token =>
                token.TokenHash == tokenHash &&
                token.JwtId == jwtId &&
                token.ConnectionId == connectionId,
                context.HttpContext.RequestAborted);

        var connection = storedToken?.Connection;
        if (storedToken is null ||
            storedToken.RevokedAt is not null ||
            storedToken.ExpiresAt <= now ||
            connection is null ||
            connection.Status != HouseholdConnection.ActiveStatus ||
            connection.RevokedAt is not null ||
            connection.User is null ||
            !connection.User.IsActive)
        {
            context.Fail("Invalid integration access token.");
            return;
        }

        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, connection.UserId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Name, connection.User.DisplayName));
        }

        connection.LastUsedAt = now;
        connection.UpdatedAt = now;
        await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
    }

    private static string? ReadBearerToken(HttpRequest request)
    {
        var value = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = value[prefix.Length..].Trim();
        return token.Length is > 0 and <= 8192 ? token : null;
    }
}
