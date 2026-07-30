using System.Text.Json;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Configuration;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Application.Services;

public sealed class NotificationDispatcher(
    DoItDbContext dbContext,
    IWebPushSender webPushSender,
    ITaskNotificationService taskNotificationService,
    IOptions<WebPushSettings> webPushOptions,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public const string CalendarReminderSourceType = "CalendarEventReminder";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebPushSettings _settings = webPushOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            return 0;
        }

        if (!_settings.HasVapidConfiguration)
        {
            logger.LogError("Web Push is enabled but WebPush:PublicKey, WebPush:PrivateKey and WebPush:Subject must all be configured. No notifications will be sent.");
            return 0;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await EnsureCalendarReminderDeliveriesAsync(now, cancellationToken);
        await taskNotificationService.EnsureScheduledDeliveriesAsync(now, cancellationToken);
        return await ProcessDeliveriesAsync(now, cancellationToken);
    }

    private async Task EnsureCalendarReminderDeliveriesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var lookbackSeconds = Math.Max(0, _settings.LookbackSeconds);
        var from = now.AddSeconds(-lookbackSeconds);
        var reminders = await dbContext.CalendarEventReminders
            .AsNoTracking()
            .Include(reminder => reminder.CalendarEvent)
            .Where(reminder => reminder.IsEnabled
                && reminder.AcknowledgedAt == null
                && reminder.CalendarEvent != null
                && !reminder.CalendarEvent.IsCancelled)
            .ToListAsync(cancellationToken);

        var subscriptionsByUser = new Dictionary<Guid, IReadOnlyList<PushSubscription>>();
        var newDeliveries = new List<NotificationDelivery>();
        foreach (var reminder in reminders)
        {
            if (reminder.CalendarEvent is null)
            {
                continue;
            }

            var dueAtUtc = CalculateDueAtUtc(reminder);
            if (dueAtUtc < from || dueAtUtc > now)
            {
                continue;
            }

            if (!subscriptionsByUser.TryGetValue(reminder.CalendarEvent.CreatedByUserId, out var subscriptions))
            {
                subscriptions = await dbContext.PushSubscriptions
                    .AsNoTracking()
                    .Where(subscription => subscription.UserId == reminder.CalendarEvent.CreatedByUserId && subscription.IsActive)
                    .ToListAsync(cancellationToken);
                subscriptionsByUser[reminder.CalendarEvent.CreatedByUserId] = subscriptions;
            }

            var deduplicationKey = BuildDeduplicationKey(reminder.Id, dueAtUtc);
            foreach (var subscription in subscriptions)
            {
                var exists = await dbContext.NotificationDeliveries.AnyAsync(delivery =>
                    delivery.PushSubscriptionId == subscription.Id
                    && delivery.DeduplicationKey == deduplicationKey, cancellationToken);
                if (exists)
                {
                    continue;
                }

                newDeliveries.Add(new NotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    UserId = reminder.CalendarEvent.CreatedByUserId,
                    PushSubscriptionId = subscription.Id,
                    SourceType = CalendarReminderSourceType,
                    SourceId = reminder.Id,
                    DeduplicationKey = deduplicationKey,
                    DueAtUtc = dueAtUtc,
                    Status = NotificationDeliveryStatus.Pending,
                    AttemptCount = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        if (newDeliveries.Count == 0)
        {
            return;
        }

        dbContext.NotificationDeliveries.AddRange(newDeliveries);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            foreach (var delivery in newDeliveries)
            {
                dbContext.Entry(delivery).State = EntityState.Detached;
            }

            logger.LogDebug(exception, "A Web Push delivery was inserted concurrently; the unique deduplication index will keep one delivery.");
        }
    }

    private async Task<int> ProcessDeliveriesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, _settings.BatchSize);
        var maxAttempts = Math.Max(1, _settings.MaxAttempts);
        var candidateIds = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(delivery => (delivery.SourceType == CalendarReminderSourceType
                    || delivery.SourceType == TaskNotificationService.AvailableFromSourceType
                    || delivery.SourceType == TaskNotificationService.RecommendedSourceType
                    || delivery.SourceType == TaskNotificationService.BeforeAvailableUntilSourceType
                    || delivery.SourceType == TaskNotificationService.CompletedSourceType)
                && delivery.DueAtUtc <= now
                && delivery.AttemptCount < maxAttempts
                && (delivery.NextAttemptAtUtc == null || delivery.NextAttemptAtUtc <= now)
                && (delivery.Status == NotificationDeliveryStatus.Pending
                    || delivery.Status == NotificationDeliveryStatus.Failed
                    || (delivery.Status == NotificationDeliveryStatus.Processing
                        && (delivery.LeaseUntilUtc == null || delivery.LeaseUntilUtc <= now))))
            .OrderBy(delivery => delivery.DueAtUtc)
            .ThenBy(delivery => delivery.CreatedAt)
            .Take(batchSize)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var candidateId in candidateIds)
        {
            if (!await ClaimDeliveryAsync(candidateId, now, maxAttempts, cancellationToken))
            {
                continue;
            }

            processed++;
            await ProcessClaimedDeliveryAsync(candidateId, now, maxAttempts, cancellationToken);
        }

        return processed;
    }

    private async Task<bool> ClaimDeliveryAsync(
        Guid deliveryId,
        DateTime now,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.NotificationDeliveries
            .FirstOrDefaultAsync(candidate => candidate.Id == deliveryId, cancellationToken);
        if (delivery is null
            || delivery.AttemptCount >= maxAttempts
            || delivery.NextAttemptAtUtc > now
            || (delivery.Status == NotificationDeliveryStatus.Processing
                && delivery.LeaseUntilUtc is not null
                && delivery.LeaseUntilUtc > now)
            || (delivery.Status != NotificationDeliveryStatus.Pending
                && delivery.Status != NotificationDeliveryStatus.Failed
                && delivery.Status != NotificationDeliveryStatus.Processing))
        {
            return false;
        }

        delivery.Status = NotificationDeliveryStatus.Processing;
        delivery.AttemptCount++;
        delivery.LeaseUntilUtc = now.AddMinutes(2);
        delivery.NextAttemptAtUtc = null;
        delivery.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return true;
    }

    private async Task ProcessClaimedDeliveryAsync(
        Guid deliveryId,
        DateTime now,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.NotificationDeliveries
            .Include(candidate => candidate.PushSubscription)
            .FirstOrDefaultAsync(candidate => candidate.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return;
        }

        string? payload;
        if (delivery.SourceType == CalendarReminderSourceType)
        {
            var reminder = await dbContext.CalendarEventReminders
                .AsNoTracking()
                .Include(candidate => candidate.CalendarEvent)
                .FirstOrDefaultAsync(candidate => candidate.Id == delivery.SourceId, cancellationToken);

            if (reminder?.CalendarEvent is null || !IsStillEligible(delivery, reminder, now))
            {
                await MarkTerminalFailureAsync(delivery, now, maxAttempts, "Calendar reminder is no longer eligible.", cancellationToken);
                return;
            }

            if (delivery.PushSubscription is null
                || !delivery.PushSubscription.IsActive
                || delivery.PushSubscription.UserId != reminder.CalendarEvent.CreatedByUserId
                || delivery.UserId != reminder.CalendarEvent.CreatedByUserId)
            {
                await MarkTerminalFailureAsync(delivery, now, maxAttempts, "Push subscription is no longer active.", cancellationToken);
                return;
            }

            payload = BuildPayload(reminder, CalculateDueAtUtc(reminder));
        }
        else if (TaskNotificationService.IsTaskSourceType(delivery.SourceType))
        {
            payload = await taskNotificationService.BuildPayloadIfEligibleAsync(delivery, now, cancellationToken);
            if (payload is null)
            {
                await MarkTerminalFailureAsync(delivery, now, maxAttempts, "Task notification is no longer eligible.", cancellationToken);
                return;
            }

            if (delivery.PushSubscription is null
                || !delivery.PushSubscription.IsActive
                || delivery.PushSubscription.UserId != delivery.UserId)
            {
                await MarkTerminalFailureAsync(delivery, now, maxAttempts, "Push subscription is no longer active.", cancellationToken);
                return;
            }
        }
        else
        {
            await MarkTerminalFailureAsync(delivery, now, maxAttempts, "Notification source is not supported.", cancellationToken);
            return;
        }

        var pushSubscription = delivery.PushSubscription!;
        WebPushSendResult sendResult;
        try
        {
            sendResult = await webPushSender.SendAsync(pushSubscription, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            sendResult = WebPushSendResult.Failure(exception.Message);
        }
        if (sendResult.Succeeded)
        {
            delivery.Status = NotificationDeliveryStatus.Sent;
            delivery.LeaseUntilUtc = null;
            delivery.NextAttemptAtUtc = null;
            delivery.SentAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            delivery.LastError = null;
            delivery.UpdatedAt = delivery.SentAtUtc.Value;
            pushSubscription.LastSeenAt = delivery.UpdatedAt;
            pushSubscription.UpdatedAt = delivery.UpdatedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (sendResult.IsExpired)
        {
            pushSubscription.IsActive = false;
            pushSubscription.UpdatedAt = now;
            await MarkTerminalFailureAsync(delivery, now, maxAttempts, sendResult.Error ?? "Push subscription is no longer valid.", cancellationToken);
            return;
        }

        delivery.Status = NotificationDeliveryStatus.Failed;
        delivery.LeaseUntilUtc = null;
        delivery.LastError = TruncateError(sendResult.Error ?? "Web Push send failed.");
        delivery.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        delivery.NextAttemptAtUtc = delivery.AttemptCount >= maxAttempts
            ? null
            : delivery.UpdatedAt.Add(GetBackoff(delivery.AttemptCount));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkTerminalFailureAsync(
        NotificationDelivery delivery,
        DateTime now,
        int maxAttempts,
        string error,
        CancellationToken cancellationToken)
    {
        delivery.Status = NotificationDeliveryStatus.Failed;
        delivery.AttemptCount = Math.Max(delivery.AttemptCount, maxAttempts);
        delivery.LeaseUntilUtc = null;
        delivery.NextAttemptAtUtc = null;
        delivery.LastError = TruncateError(error);
        delivery.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private static bool IsStillEligible(NotificationDelivery delivery, CalendarEventReminder reminder, DateTime now)
    {
        if (!reminder.IsEnabled
            || reminder.AcknowledgedAt is not null
            || reminder.CalendarEvent is null
            || reminder.CalendarEvent.IsCancelled)
        {
            return false;
        }

        var dueAtUtc = CalculateDueAtUtc(reminder);
        return dueAtUtc == delivery.DueAtUtc && dueAtUtc <= now;
    }

    private static DateTime CalculateDueAtUtc(CalendarEventReminder reminder)
    {
        var dueAtUtc = reminder.CalendarEvent!.StartAtUtc.AddMinutes(-reminder.OffsetMinutes);
        return DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc);
    }

    private static string BuildDeduplicationKey(Guid reminderId, DateTime dueAtUtc) =>
        $"calendar-reminder:{reminderId}:{dueAtUtc.Ticks}";

    private static string BuildPayload(CalendarEventReminder reminder, DateTime dueAtUtc)
    {
        var calendarEvent = reminder.CalendarEvent!;
        var body = BuildBody(calendarEvent);
        var tag = BuildDeduplicationKey(reminder.Id, dueAtUtc);
        var payload = new
        {
            title = $"Recordatorio: {calendarEvent.Title}",
            body,
            url = "/calendar",
            tag,
            data = new
            {
                sourceType = CalendarReminderSourceType,
                sourceId = reminder.Id,
                eventId = calendarEvent.Id,
                dueAtUtc
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildBody(CalendarEvent calendarEvent)
    {
        try
        {
            var startAtUtc = DateTime.SpecifyKind(calendarEvent.StartAtUtc, DateTimeKind.Utc);
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(startAtUtc, TimeZoneHelper.Find(calendarEvent.TimeZoneId));
            return $"Evento programado para {localStart:dd/MM/yyyy HH:mm}.";
        }
        catch (TimeZoneNotFoundException)
        {
            return "Tienes un evento pendiente";
        }
        catch (InvalidTimeZoneException)
        {
            return "Tienes un evento pendiente";
        }
    }

    private static TimeSpan GetBackoff(int attemptCount)
    {
        var seconds = Math.Min(900, 5 * Math.Pow(2, Math.Min(8, Math.Max(0, attemptCount - 1))));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string TruncateError(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
