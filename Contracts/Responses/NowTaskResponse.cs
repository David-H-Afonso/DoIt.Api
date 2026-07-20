namespace DoIt.Api.Contracts.Responses;

public sealed record NowTaskResponse(
    Guid OccurrenceId,
    Guid Id,
    string Title,
    Guid? ZoneId,
    string? ZoneName,
    string Scope,
    string AssignmentMode,
    IReadOnlyList<Guid> AssigneeIds,
    IReadOnlyList<string> AssigneeNames,
    string Status,
    string OccurrenceStatus,
    DateOnly OccurrenceDate,
    string? CompletionTiming,
    DateTime? CompletedAt,
    TimeOnly? AvailableFromTime,
    TimeOnly? AvailableUntilTime,
    TimeOnly? RecommendedTime,
    string TimeZoneId);
