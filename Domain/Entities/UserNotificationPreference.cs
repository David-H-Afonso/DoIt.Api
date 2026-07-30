namespace DoIt.Api.Domain.Entities;

public sealed class UserNotificationPreference
{
    public Guid UserId { get; set; }
    public bool AvailableFromEnabled { get; set; } = true;
    public bool RecommendedEnabled { get; set; } = true;
    public bool BeforeAvailableUntilEnabled { get; set; } = true;
    public bool TaskCompletedEnabled { get; set; } = true;
    public int BeforeAvailableUntilMinutes { get; set; } = 30;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
