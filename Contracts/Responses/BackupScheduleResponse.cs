namespace DoIt.Api.Contracts.Responses;

public sealed record BackupScheduleResponse(
    Guid UserId,
    string Username,
    string DestinationPath,
    int RetentionCount,
    string FileNamePrefix,
    string FileNameSuffix,
    DateTime? LastRunAt,
    string LastRunStatus,
    string? LastRunMessage);
