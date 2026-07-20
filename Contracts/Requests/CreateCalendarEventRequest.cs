namespace DoIt.Api.Contracts.Requests;

public sealed record CreateCalendarEventRequest(
    string Title,
    string? Description,
    Guid? ZoneId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsAllDay,
    string? TimeZoneId,
    IReadOnlyList<CalendarReminderRequest>? Reminders);
