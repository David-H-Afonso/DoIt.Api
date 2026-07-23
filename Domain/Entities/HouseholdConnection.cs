namespace DoIt.Api.Domain.Entities;

public sealed class HouseholdConnection
{
    public const string ActiveStatus = "Active";
    public const string RevokedStatus = "Revoked";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string GrantedScopes { get; set; } = string.Empty;
    public string Status { get; set; } = ActiveStatus;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User? User { get; set; }
    public ICollection<HouseholdAuthorizationCode> AuthorizationCodes { get; set; } = new List<HouseholdAuthorizationCode>();
    public ICollection<HouseholdRefreshToken> RefreshTokens { get; set; } = new List<HouseholdRefreshToken>();
    public ICollection<HouseholdAccessToken> AccessTokens { get; set; } = new List<HouseholdAccessToken>();
}
