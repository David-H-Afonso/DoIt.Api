namespace DoIt.Api.Contracts.Requests;

public sealed record TaskScheduleRequest(
    string? RecurrenceType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DayOfWeek? Weekday,
    int? TimesPerWeek,
    int? EveryNDays,
    TimeOnly? AvailableFromTime,
    TimeOnly? AvailableUntilTime,
    TimeOnly? RecommendedTime,
    string? UnavailableVisibilityMode,
    string? TimeZoneId = null,
    int? WeekOfMonth = null,
    int? Interval = null);
