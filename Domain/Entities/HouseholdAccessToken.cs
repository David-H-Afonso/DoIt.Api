namespace DoIt.Api.Domain.Entities;

public sealed class HouseholdAccessToken
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public HouseholdConnection? Connection { get; set; }
}
