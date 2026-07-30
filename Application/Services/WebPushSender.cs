using DoIt.Api.Application.Interfaces;
using DoIt.Api.Configuration;
using DoIt.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using DomainPushSubscription = DoIt.Api.Domain.Entities.PushSubscription;
using VapidDetails = WebPush.VapidDetails;
using WebPushClient = WebPush.WebPushClient;
using WebPushException = WebPush.WebPushException;

namespace DoIt.Api.Application.Services;

public sealed class WebPushSender(
    IOptions<WebPushSettings> webPushOptions,
    ILogger<WebPushSender> logger) : IWebPushSender, IDisposable
{
    private readonly WebPushSettings _settings = webPushOptions.Value;
    private readonly WebPushClient _client = new();

    public async Task<WebPushSendResult> SendAsync(
        DomainPushSubscription subscription,
        string payload,
        CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            return WebPushSendResult.Failure("Web Push is disabled.");
        }

        if (!_settings.HasVapidConfiguration)
        {
            logger.LogError("Web Push is enabled but WebPush:PublicKey, WebPush:PrivateKey and WebPush:Subject must all be configured. No notifications will be sent.");
            return WebPushSendResult.Failure("Web Push VAPID configuration is incomplete.");
        }

        try
        {
            var webPushSubscription = new WebPush.PushSubscription(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth);
            var vapidDetails = new VapidDetails(
                _settings.Subject.Trim(),
                _settings.PublicKey.Trim(),
                _settings.PrivateKey.Trim());

            await _client.SendNotificationAsync(
                webPushSubscription,
                payload,
                vapidDetails,
                cancellationToken);

            return WebPushSendResult.Success();
        }
        catch (WebPushException exception)
        {
            return WebPushSendResult.Failure(exception.Message, (int)exception.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return WebPushSendResult.Failure(exception.Message);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
