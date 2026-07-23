namespace DoIt.Api.Configuration;

public sealed class HouseholdIntegrationSettings
{
    public const string SectionName = "HouseholdIntegration";

    public string ClientId { get; init; } = "household";
    public string RedirectUris { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
    public int AuthorizationCodeMinutes { get; init; } = 5;

    public IReadOnlySet<string> ParsedRedirectUris => RedirectUris
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);
}
