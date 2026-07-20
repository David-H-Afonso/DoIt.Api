namespace DoIt.Api.Contracts.Responses;

public sealed record StatisticsResponse(
    DateOnly From,
    DateOnly To,
    string GroupBy,
    StatisticsSummaryResponse Summary,
    IReadOnlyList<StatisticsBucketResponse> Buckets,
    IReadOnlyList<TaskStatisticsResponse> Tasks);

public sealed record StatisticsSummaryResponse(
    int Scheduled,
    int Completed,
    int CompletedEarly,
    int CompletedLate,
    int CompletedOverdue,
    int Missed,
    int NotApplicable,
    int Pending,
    decimal CompletionRate);

public sealed record StatisticsBucketResponse(
    string Key,
    DateOnly From,
    DateOnly To,
    StatisticsSummaryResponse Summary);

public sealed record TaskStatisticsResponse(
    Guid TaskId,
    string Title,
    string? ZoneName,
    string RecurrenceType,
    StatisticsSummaryResponse Summary,
    IReadOnlyList<StatisticsOccurrenceResponse> Occurrences);

public sealed record StatisticsOccurrenceResponse(
    Guid? OccurrenceId,
    Guid TaskId,
    DateOnly ScheduledDate,
    string Status,
    string Timing,
    DateTime? CompletedAt,
    int? DifferenceMinutes,
    string TimeZoneId);
