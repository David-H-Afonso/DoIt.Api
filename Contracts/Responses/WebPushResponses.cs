namespace DoIt.Api.Contracts.Responses;

public sealed record WebPushConfigResponse(
    bool Enabled,
    string? PublicKey);

public sealed record PushSubscriptionStatusResponse(
    Guid Id,
    string Endpoint,
    string? DeviceName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastSeenAt);
