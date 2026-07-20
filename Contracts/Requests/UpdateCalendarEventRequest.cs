namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateCalendarEventRequest(
    string Title,
    string? Description,
    Guid? ZoneId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsAllDay,
    string? TimeZoneId,
    bool IsCancelled,
    IReadOnlyList<CalendarReminderRequest>? Reminders);
