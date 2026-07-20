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
    TimeOnly? AvailableFromTime,
    TimeOnly? AvailableUntilTime,
    TimeOnly? RecommendedTime,
    string TimeZoneId);
