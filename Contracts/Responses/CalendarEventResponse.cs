namespace DoIt.Api.Contracts.Responses;

public sealed record CalendarReminderResponse(
    Guid Id,
    int OffsetMinutes,
    bool IsEnabled,
    DateTime? AcknowledgedAt,
    DateTime DueAtUtc);

public sealed record CalendarEventResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid? ZoneId,
    string? ZoneName,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    bool IsAllDay,
    string TimeZoneId,
    bool IsCancelled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CalendarReminderResponse> Reminders);

public sealed record CalendarReminderDueResponse(
    Guid ReminderId,
    Guid EventId,
    string EventTitle,
    DateTime StartAtUtc,
    int OffsetMinutes,
    DateTime DueAtUtc);

public sealed record CalendarMonthlyReportResponse(
    int Year,
    int Month,
    int TotalEvents,
    int ActiveEvents,
    int CancelledEvents,
    int EnabledReminders,
    int AcknowledgedReminders);
