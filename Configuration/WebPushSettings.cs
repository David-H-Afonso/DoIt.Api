namespace DoIt.Api.Configuration;

public sealed class WebPushSettings
{
    public const string SectionName = "WebPush";

    public bool Enabled { get; init; }
    public string PublicKey { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public int WorkerIntervalSeconds { get; init; } = 30;
    public int LookbackSeconds { get; init; } = 120;
    public int BatchSize { get; init; } = 100;
    public int MaxAttempts { get; init; } = 3;

    public bool HasVapidConfiguration =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
