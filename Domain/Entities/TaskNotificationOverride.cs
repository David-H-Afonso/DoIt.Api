namespace DoIt.Api.Domain.Entities;

public sealed class TaskNotificationOverride
{
    public Guid TaskId { get; set; }
    public bool? AvailableFromEnabled { get; set; }
    public bool? RecommendedEnabled { get; set; }
    public bool? BeforeAvailableUntilEnabled { get; set; }
    public bool? TaskCompletedEnabled { get; set; }
    public int? BeforeAvailableUntilMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DoItTask? Task { get; set; }
}
