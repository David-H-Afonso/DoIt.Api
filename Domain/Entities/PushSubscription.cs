namespace DoIt.Api.Domain.Entities;

public sealed class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public User? User { get; set; }
    public ICollection<NotificationDelivery> NotificationDeliveries { get; set; } = new List<NotificationDelivery>();
}
