using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateBackupScheduleRequest(
    [Required] string DestinationPath,
    int RetentionCount,
    string? FileNamePrefix,
    string? FileNameSuffix);
