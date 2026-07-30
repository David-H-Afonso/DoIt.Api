using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PushSubscriptionId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public PushSubscription? PushSubscription { get; set; }
}
