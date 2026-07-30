namespace DoIt.Api.Contracts.Requests;

public sealed record PushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? DeviceName = null);

public sealed record DeletePushSubscriptionRequest(
    string? Endpoint = null,
    Guid? Id = null);
