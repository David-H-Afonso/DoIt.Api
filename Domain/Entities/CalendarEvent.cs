namespace DoIt.Api.Domain.Entities;

public sealed class CalendarEvent
{
    public Guid Id { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ZoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public bool IsCancelled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public Zone? Zone { get; set; }
    public ICollection<CalendarEventReminder> Reminders { get; set; } = new List<CalendarEventReminder>();
}
