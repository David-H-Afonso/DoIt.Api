namespace DoIt.Api.Domain.Entities;

public sealed class NotificationInboxItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Url { get; set; } = "/now";
    public string? DataJson { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public User? User { get; set; }
    public ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}
