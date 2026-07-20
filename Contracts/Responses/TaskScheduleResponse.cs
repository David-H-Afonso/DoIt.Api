namespace DoIt.Api.Contracts.Responses;

public sealed record TaskScheduleResponse(
    Guid Id,
    string RecurrenceType,
    DateOnly StartDate,
    DateOnly? EndDate,
    DayOfWeek? Weekday,
    int? WeekOfMonth,
    int? TimesPerWeek,
    int? EveryNDays,
    TimeOnly? AvailableFromTime,
    TimeOnly? AvailableUntilTime,
    TimeOnly? RecommendedTime,
    string UnavailableVisibilityMode,
    string TimeZoneId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
