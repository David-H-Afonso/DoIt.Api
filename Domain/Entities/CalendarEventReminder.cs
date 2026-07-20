namespace DoIt.Api.Domain.Entities;

public sealed class CalendarEventReminder
{
    public Guid Id { get; set; }
    public Guid CalendarEventId { get; set; }
    public int OffsetMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CalendarEvent? CalendarEvent { get; set; }
}
