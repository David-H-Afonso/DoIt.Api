namespace DoIt.Api.Configuration;

public sealed class DoItSettings
{
    public const string SectionName = "DoItSettings";

    public string DefaultLocale { get; init; } = "es";
    public string DefaultTheme { get; init; } = "system";
}
