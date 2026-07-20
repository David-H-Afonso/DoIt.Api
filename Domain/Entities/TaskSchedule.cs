using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class TaskSchedule
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.Manual;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DayOfWeek? Weekday { get; set; }
    public int? WeekOfMonth { get; set; }
    public int? TimesPerWeek { get; set; }
    public int? EveryNDays { get; set; }
    public TimeOnly? AvailableFromTime { get; set; }
    public TimeOnly? AvailableUntilTime { get; set; }
    public TimeOnly? RecommendedTime { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public UnavailableVisibilityMode UnavailableVisibilityMode { get; set; } = UnavailableVisibilityMode.Dimmed;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DoItTask? Task { get; set; }
}
