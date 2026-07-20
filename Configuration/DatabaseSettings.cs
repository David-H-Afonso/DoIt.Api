namespace DoIt.Api.Configuration;

public sealed class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    public string DatabasePath { get; init; } = "../data/doit.db";
}
