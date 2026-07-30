namespace DoIt.Api.Contracts.Responses;

public sealed record NotificationPreferenceResponse(
    bool AvailableFromEnabled,
    bool RecommendedEnabled,
    bool BeforeAvailableUntilEnabled,
    bool TaskExpiredEnabled,
    bool TaskCompletedEnabled,
    int BeforeAvailableUntilMinutes);

public sealed record TaskNotificationOverrideResponse(
    Guid TaskId,
    bool? AvailableFromEnabled,
    bool? RecommendedEnabled,
    bool? BeforeAvailableUntilEnabled,
    bool? TaskExpiredEnabled,
    bool? TaskCompletedEnabled,
    int? BeforeAvailableUntilMinutes);
