namespace DoIt.Api.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "DoIt";
    public string Audience { get; init; } = "DoIt";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 365;
}
