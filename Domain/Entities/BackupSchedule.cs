namespace DoIt.Api.Domain.Entities;

public sealed class BackupSchedule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DestinationPath { get; set; } = "/app/data/backups";
    public int RetentionCount { get; set; } = 7;
    public string FileNamePrefix { get; set; } = "";
    public string FileNameSuffix { get; set; } = "";
    public DateTime? LastRunAt { get; set; }
    public string LastRunStatus { get; set; } = "never";
    public string? LastRunMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
