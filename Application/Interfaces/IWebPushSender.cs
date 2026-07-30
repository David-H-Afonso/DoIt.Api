using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Interfaces;

public interface IWebPushSender
{
    Task<WebPushSendResult> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken);
}

public sealed record WebPushSendResult(
    bool Succeeded,
    int? StatusCode = null,
    string? Error = null)
{
    public bool IsExpired => StatusCode is 404 or 410;

    public static WebPushSendResult Success() => new(true);
    public static WebPushSendResult Failure(string error, int? statusCode = null) => new(false, statusCode, error);
}
