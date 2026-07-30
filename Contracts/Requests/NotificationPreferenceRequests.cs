namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateNotificationPreferencesRequest(
    bool? AvailableFromEnabled,
    bool? RecommendedEnabled,
    bool? BeforeAvailableUntilEnabled,
    bool? TaskExpiredEnabled,
    bool? TaskCompletedEnabled,
    int? BeforeAvailableUntilMinutes);

public sealed record UpdateTaskNotificationOverrideRequest(
    bool? AvailableFromEnabled,
    bool? RecommendedEnabled,
    bool? BeforeAvailableUntilEnabled,
    bool? TaskExpiredEnabled,
    bool? TaskCompletedEnabled,
    int? BeforeAvailableUntilMinutes);
