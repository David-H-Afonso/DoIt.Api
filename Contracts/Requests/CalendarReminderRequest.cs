namespace DoIt.Api.Contracts.Requests;

public sealed record CalendarReminderRequest(int OffsetMinutes, bool IsEnabled = true);
