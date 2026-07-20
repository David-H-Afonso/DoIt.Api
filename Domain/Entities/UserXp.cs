namespace DoIt.Api.Domain.Entities;

public sealed class UserXp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int TotalXp { get; set; }
    public int WeeklyXp { get; set; }
    public int CurrentLevel { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
