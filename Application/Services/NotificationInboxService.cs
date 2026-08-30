using System.Text.Json;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Configuration;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Application.Services;

public sealed class NotificationInboxService(
    DoItDbContext dbContext,
    IOptions<WebPushSettings> webPushOptions,
    TimeProvider timeProvider,
    ILogger<NotificationInboxService> logger) : INotificationInboxService
{
    private const int RetentionDays = 30;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebPushSettings _settings = webPushOptions.Value;

    public async Task<IReadOnlyList<NotificationInboxItemResponse>> ListAsync(Guid userId, bool unreadOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.NotificationInboxItems.AsNoTracking().Where(item => item.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(item => item.ReadAtUtc == null);
        }

        var items = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToResponse).ToList();
    }

    public async Task<NotificationInboxItemResponse> GetAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await dbContext.NotificationInboxItems.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.UserId == userId, cancellationToken);
        return item is null
            ? throw new ApiException(StatusCodes.Status404NotFound, "notification_not_found", "Notification not found.")
            : ToResponse(item);
    }

    public async Task<NotificationInboxItemResponse> CreateAsync(
        Guid userId,
        string sourceType,
        Guid sourceId,
        string deduplicationKey,
        string title,
        string body,
        string url,
        object? data,
        DateTime dueAtUtc,
        bool queuePush,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.NotificationInboxItems
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.DeduplicationKey == deduplicationKey, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (item is null)
        {
            item = new NotificationInboxItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                DeduplicationKey = deduplicationKey,
                Title = title,
                Body = body,
                Url = url,
                DataJson = data is null ? null : JsonSerializer.Serialize(data, JsonOptions),
                DueAtUtc = DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc),
                CreatedAt = now
            };
            dbContext.NotificationInboxItems.Add(item);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                dbContext.Entry(item).State = EntityState.Detached;
                item = await dbContext.NotificationInboxItems.FirstAsync(candidate => candidate.UserId == userId && candidate.DeduplicationKey == deduplicationKey, cancellationToken);
                logger.LogDebug(exception, "An inbox notification was inserted concurrently; the unique key kept one item.");
            }
        }

        if (queuePush)
        {
            await QueuePushAsync(item, cancellationToken);
        }

        return ToResponse(item);
    }

    public async Task MarkReadAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await dbContext.NotificationInboxItems
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.UserId == userId, cancellationToken);
        if (item is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "notification_not_found", "Notification not found.");
        }

        item.ReadAtUtc ??= timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.NotificationInboxItems
            .Where(item => item.UserId == userId && item.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAtUtc, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationInboxItemResponse>> ListForPushGroupAsync(Guid userId, string groupKey, CancellationToken cancellationToken)
    {
        var parts = groupKey.Split(':');
        if (parts.Length != 3 || !long.TryParse(parts[2], out var bucket))
        {
            return [];
        }

        var start = DateTimeOffset.FromUnixTimeSeconds(bucket * 120).UtcDateTime;
        var end = start.AddMinutes(2);
        var items = await dbContext.NotificationInboxItems.AsNoTracking()
            .Where(item => item.UserId == userId && item.DueAtUtc >= start && item.DueAtUtc < end)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(ToResponse).ToList();
    }

    private async Task QueuePushAsync(NotificationInboxItem item, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || !_settings.HasVapidConfiguration)
        {
            return;
        }

        var subscriptions = await dbContext.PushSubscriptions
            .Where(subscription => subscription.UserId == item.UserId && subscription.IsActive)
            .ToListAsync(cancellationToken);
        var newDeliveries = new List<NotificationDelivery>();
        foreach (var subscription in subscriptions)
        {
            var groupKey = BuildPushGroupKey(item.UserId, item.DueAtUtc);
            var key = $"inbox:{groupKey}:{subscription.Id:N}";
            if (await dbContext.NotificationDeliveries.AnyAsync(delivery => delivery.PushSubscriptionId == subscription.Id && delivery.PushGroupKey == groupKey, cancellationToken))
            {
                continue;
            }

            newDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                UserId = item.UserId,
                PushSubscriptionId = subscription.Id,
                NotificationInboxItemId = item.Id,
                PushGroupKey = groupKey,
                SourceType = item.SourceType,
                SourceId = item.SourceId,
                DeduplicationKey = key,
                // Delay the delivery until the two-minute bucket closes so later events can join this Push.
                DueAtUtc = GetPushGroupDueAt(item.DueAtUtc),
                Status = NotificationDeliveryStatus.Pending,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.CreatedAt
            });
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
            logger.LogDebug(exception, "An inbox Push delivery was inserted concurrently; the unique key kept one delivery.");
        }
    }

    public static string BuildPushGroupKey(Guid userId, DateTime dueAtUtc)
    {
        var bucket = new DateTimeOffset(DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds() / 120;
        return $"push:{userId:N}:{bucket}";
    }

    public async Task PruneExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc).AddDays(-RetentionDays);
        await dbContext.NotificationInboxItems
            .Where(item => item.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public static DateTime GetPushGroupDueAt(DateTime dueAtUtc)
    {
        var utc = new DateTimeOffset(DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc));
        var bucket = utc.ToUnixTimeSeconds() / 120;
        return DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds((bucket + 1) * 120).UtcDateTime, DateTimeKind.Utc);
    }

    private static NotificationInboxItemResponse ToResponse(NotificationInboxItem item)
    {
        object? data = null;
        if (!string.IsNullOrWhiteSpace(item.DataJson))
        {
            try { data = JsonSerializer.Deserialize<JsonElement>(item.DataJson, JsonOptions); }
            catch (JsonException) { }
        }

        return new NotificationInboxItemResponse(item.Id, item.SourceType, item.SourceId, item.Title, item.Body, item.Url, data, item.DueAtUtc, item.CreatedAt, item.ReadAtUtc is not null, item.ReadAtUtc);
    }
}
