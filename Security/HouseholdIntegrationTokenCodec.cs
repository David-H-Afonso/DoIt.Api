using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DoIt.Api.Configuration;
using DoIt.Api.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DoIt.Api.Security;

public sealed class HouseholdIntegrationTokenCodec(
    IOptions<JwtSettings> jwtOptions,
    IOptions<HouseholdIntegrationSettings> integrationOptions)
{
    private const string TokenUse = "household_integration";
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;
    private readonly HouseholdIntegrationSettings _integrationSettings = integrationOptions.Value;

    public static string Issuer(JwtSettings settings) => $"{settings.Issuer}.HouseholdIntegration.v1";

    public static SymmetricSecurityKey SigningKey(JwtSettings settings)
    {
        var sourceKey = Encoding.UTF8.GetBytes(settings.SecretKey);
        var derivedKey = HMACSHA256.HashData(sourceKey, Encoding.UTF8.GetBytes("DoIt/HouseholdIntegration/v1/access-token"));
        return new SymmetricSecurityKey(derivedKey);
    }

    public (string RawToken, HouseholdAccessToken StoredToken) CreateAccessToken(
        HouseholdConnection connection,
        Guid familyId,
        DateTime now,
        DateTime expiresAt)
    {
        var jwtId = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, connection.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new("connection_id", connection.Id.ToString()),
            new("client_id", connection.ClientId),
            new("token_use", TokenUse)
        };
        claims.AddRange(ParseScopes(connection.GrantedScopes).Select(scope => new Claim("scope", scope)));

        var token = new JwtSecurityToken(
            Issuer(_jwtSettings),
            _integrationSettings.ClientId,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(SigningKey(_jwtSettings), SecurityAlgorithms.HmacSha256));
        token.Header[JwtHeaderParameterNames.Typ] = "at+jwt";

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (rawToken, new HouseholdAccessToken
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            TokenHash = HashCredential(rawToken),
            JwtId = jwtId,
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
    }

    public string CreateAccountId(Guid userId)
        => $"doit_{userId:N}";

    public static string CreateCredential(int byteCount) => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    public static string HashCredential(string credential) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));

    public static IReadOnlyCollection<string> ParseScopes(string scopes) => scopes
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
