using DoIt.Api.Application.Interfaces;
using DoIt.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Background;

public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WebPushSettings> webPushOptions,
    TimeProvider timeProvider,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private readonly WebPushSettings _settings = webPushOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("Web Push notification worker is disabled.");
            return;
        }

        if (!_settings.HasVapidConfiguration)
        {
            logger.LogError("Web Push is enabled but WebPush:PublicKey, WebPush:PrivateKey and WebPush:Subject must all be configured. The notification worker will not send notifications.");
            return;
        }

        await DispatchOnceAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.WorkerIntervalSeconds));
        using var timer = new PeriodicTimer(interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DispatchOnceAsync(stoppingToken);
        }
    }

    private async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
            await dispatcher.DispatchAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Web Push notification dispatch failed.");
        }
    }
}
