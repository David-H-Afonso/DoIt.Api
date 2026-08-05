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

public sealed class TaskNotificationService(
    DoItDbContext dbContext,
    IOccurrenceService occurrenceService,
    IOptions<WebPushSettings> webPushOptions,
    TimeProvider timeProvider,
    ILogger<TaskNotificationService> logger) : ITaskNotificationService
{
    public const int MinimumEndOffsetMinutes = 0;
    public const int MaximumEndOffsetMinutes = 1440;
    public const string AvailableFromSourceType = "TaskAvailableFrom";
    public const string RecommendedSourceType = "TaskRecommended";
    public const string BeforeAvailableUntilSourceType = "TaskBeforeAvailableUntil";
    public const string ExpiredSourceType = "TaskExpired";
    public const string CompletedSourceType = "TaskCompleted";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebPushSettings _settings = webPushOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public static bool IsTaskSourceType(string sourceType) => sourceType is
        AvailableFromSourceType or
        RecommendedSourceType or
        BeforeAvailableUntilSourceType or
        ExpiredSourceType or
        CompletedSourceType;

    public async Task EnsureScheduledDeliveriesAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (!CanQueueNotifications())
        {
            return;
        }

        var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        var lookback = TimeSpan.FromSeconds(Math.Max(0, _settings.LookbackSeconds));
        var fromUtc = nowUtc.Subtract(lookback);
        var activeUserIds = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        if (activeUserIds.Count == 0)
        {
            return;
        }

        var tasks = await dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .Include(task => task.NotificationOverride)
            .Where(task => !task.IsArchived && task.Schedule != null)
            .ToListAsync(cancellationToken);

        var activeSubscriptions = await dbContext.PushSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.IsActive && activeUserIds.Contains(subscription.UserId))
            .ToListAsync(cancellationToken);
        var subscriptionsByUser = activeSubscriptions
            .GroupBy(subscription => subscription.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());
        if (subscriptionsByUser.Count == 0)
        {
            return;
        }

        var preferencesByUser = await LoadPreferencesAsync(subscriptionsByUser.Keys, cancellationToken);
        var existingKeys = await LoadExistingTaskDeliveryKeysAsync(
            activeSubscriptions.Select(subscription => subscription.Id),
            cancellationToken);
        var newDeliveries = new List<NotificationDelivery>();

        foreach (var task in tasks)
        {
            var recipientIds = ResolveTaskRecipients(task, activeUserIds);
            if (recipientIds.Count == 0 || task.Schedule is null)
            {
                continue;
            }

            var occurrences = await GetCandidateOccurrencesAsync(task, fromUtc, nowUtc, cancellationToken);
            foreach (var occurrence in occurrences)
            {
                if (occurrence.Status != OccurrenceStatus.Pending)
                {
                    continue;
                }

                foreach (var recipientId in recipientIds)
                {
                    if (!subscriptionsByUser.TryGetValue(recipientId, out var subscriptions))
                    {
                        continue;
                    }

                    var preference = preferencesByUser[recipientId];
                    QueueScheduledDeliveries(
                        task,
                        occurrence,
                        recipientId,
                        subscriptions,
                        preference,
                        fromUtc,
                        nowUtc,
                        existingKeys,
                        newDeliveries);
                }
            }
        }

        await SaveNewDeliveriesAsync(newDeliveries, cancellationToken);
    }

    public async Task QueueTaskCompletedAsync(
        TaskOccurrence occurrence,
        TaskCompletion completion,
        CancellationToken cancellationToken)
    {
        if (!CanQueueNotifications() || occurrence.Status != OccurrenceStatus.Done)
        {
            return;
        }

        var task = await dbContext.Tasks
            .AsNoTracking()
            .Include(candidate => candidate.Assignments)
            .Include(candidate => candidate.NotificationOverride)
            .FirstOrDefaultAsync(candidate => candidate.Id == occurrence.TaskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        var activeUserIds = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        var recipientIds = task.Scope == TaskScope.House
            ? activeUserIds.Where(userId => userId != completion.UserId).ToList()
            : activeUserIds.Contains(task.CreatedByUserId) && task.CreatedByUserId != completion.UserId
                ? [task.CreatedByUserId]
                : [];
        if (recipientIds.Count == 0)
        {
            return;
        }

        var subscriptions = await dbContext.PushSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.IsActive && recipientIds.Contains(subscription.UserId))
            .ToListAsync(cancellationToken);
        var subscriptionsByUser = subscriptions
            .GroupBy(subscription => subscription.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());
        if (subscriptionsByUser.Count == 0)
        {
            return;
        }

        var preferencesByUser = await LoadPreferencesAsync(subscriptionsByUser.Keys, cancellationToken);
        var existingKeys = await LoadExistingTaskDeliveryKeysAsync(
            subscriptions.Select(subscription => subscription.Id),
            cancellationToken);
        var dueAtUtc = DateTime.SpecifyKind(completion.CreatedAt, DateTimeKind.Utc);
        var now = DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
        var newDeliveries = new List<NotificationDelivery>();

        foreach (var recipientId in recipientIds)
        {
            if (!subscriptionsByUser.TryGetValue(recipientId, out var recipientSubscriptions)
                || !IsEnabled(task.NotificationOverride?.TaskCompletedEnabled, preferencesByUser[recipientId].TaskCompletedEnabled))
            {
                continue;
            }

            foreach (var subscription in recipientSubscriptions)
            {
                var deduplicationKey = BuildDeduplicationKey(
                    CompletedSourceType,
                    occurrence.Id,
                    completion.Id,
                    recipientId,
                    subscription.Id,
                    dueAtUtc);
                AddDeliveryIfMissing(
                    newDeliveries,
                    existingKeys,
                    subscription,
                    recipientId,
                    CompletedSourceType,
                    completion.Id,
                    deduplicationKey,
                    dueAtUtc,
                    now);
            }
        }

        await SaveNewDeliveriesAsync(newDeliveries, cancellationToken);
    }

    public async Task<string?> BuildPayloadIfEligibleAsync(
        NotificationDelivery delivery,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!IsTaskSourceType(delivery.SourceType))
        {
            return null;
        }

        var nowUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        var context = delivery.SourceType == CompletedSourceType
            ? await GetCompletedDeliveryContextAsync(delivery, nowUtc, cancellationToken)
            : await GetScheduledDeliveryContextAsync(delivery, nowUtc, cancellationToken);
        return context is null ? null : BuildPayload(context);
    }

    private async Task<TaskDeliveryContext?> GetScheduledDeliveryContextAsync(
        NotificationDelivery delivery,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var occurrence = await dbContext.TaskOccurrences
            .AsNoTracking()
            .Include(candidate => candidate.Task)
            .ThenInclude(task => task!.Schedule)
            .Include(candidate => candidate.Task)
            .ThenInclude(task => task!.Assignments)
            .Include(candidate => candidate.Task)
            .ThenInclude(task => task!.NotificationOverride)
            .FirstOrDefaultAsync(candidate => candidate.Id == delivery.SourceId, cancellationToken);
        if (occurrence?.Task is null
            || occurrence.Task.Schedule is null
            || occurrence.Status != OccurrenceStatus.Pending
            || occurrence.Task.IsArchived
            || !await IsActiveUserAsync(delivery.UserId, cancellationToken)
            || !IsTaskRecipient(occurrence.Task, delivery.UserId))
        {
            return null;
        }

        var preference = await GetPreferenceAsync(delivery.UserId, cancellationToken);
        DateTime? dueAtUtc;
        int? endOffsetMinutes = null;
        bool enabled;
        switch (delivery.SourceType)
        {
            case AvailableFromSourceType:
                dueAtUtc = occurrence.AvailableFromAt;
                enabled = IsEnabled(occurrence.Task.NotificationOverride?.AvailableFromEnabled, preference.AvailableFromEnabled);
                break;
            case RecommendedSourceType:
                dueAtUtc = occurrence.RecommendedAt;
                enabled = IsEnabled(occurrence.Task.NotificationOverride?.RecommendedEnabled, preference.RecommendedEnabled);
                break;
            case BeforeAvailableUntilSourceType:
                enabled = IsEnabled(occurrence.Task.NotificationOverride?.BeforeAvailableUntilEnabled, preference.BeforeAvailableUntilEnabled);
                endOffsetMinutes = occurrence.Task.NotificationOverride?.BeforeAvailableUntilMinutes ?? preference.BeforeAvailableUntilMinutes;
                if (!IsValidEndOffset(endOffsetMinutes.Value))
                {
                    return null;
                }

                dueAtUtc = occurrence.AvailableUntilAt?.AddMinutes(-endOffsetMinutes.Value);
                break;
            case ExpiredSourceType:
                dueAtUtc = occurrence.AvailableUntilAt;
                enabled = IsEnabled(occurrence.Task.NotificationOverride?.TaskExpiredEnabled, preference.TaskExpiredEnabled);
                break;
            default:
                return null;
        }

        if (!enabled || dueAtUtc is null || dueAtUtc.Value != delivery.DueAtUtc || dueAtUtc.Value > now)
        {
            return null;
        }

        return new TaskDeliveryContext(delivery, occurrence.Task, occurrence, null, endOffsetMinutes);
    }

    private async Task<TaskDeliveryContext?> GetCompletedDeliveryContextAsync(
        NotificationDelivery delivery,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var completion = await dbContext.TaskCompletions
            .AsNoTracking()
            .Include(candidate => candidate.Occurrence)
            .ThenInclude(occurrence => occurrence!.Task)
            .ThenInclude(task => task!.Schedule)
            .Include(candidate => candidate.Occurrence)
            .ThenInclude(occurrence => occurrence!.Task)
            .ThenInclude(task => task!.Assignments)
            .Include(candidate => candidate.Occurrence)
            .ThenInclude(occurrence => occurrence!.Task)
            .ThenInclude(task => task!.NotificationOverride)
            .FirstOrDefaultAsync(candidate => candidate.Id == delivery.SourceId, cancellationToken);
        if (completion?.Occurrence?.Task is null
            || completion.Action != TaskCompletionAction.Done
            || completion.RevertedAt is not null
            || completion.Occurrence.Status != OccurrenceStatus.Done
            || completion.CreatedAt != delivery.DueAtUtc
            || completion.CreatedAt > now
            || !await IsActiveUserAsync(delivery.UserId, cancellationToken)
            || !IsCompletedRecipient(completion.Occurrence.Task, completion.UserId, delivery.UserId))
        {
            return null;
        }

        var preference = await GetPreferenceAsync(delivery.UserId, cancellationToken);
        if (!IsEnabled(completion.Occurrence.Task.NotificationOverride?.TaskCompletedEnabled, preference.TaskCompletedEnabled))
        {
            return null;
        }

        return new TaskDeliveryContext(delivery, completion.Occurrence.Task, completion.Occurrence, completion, null);
    }

    private async Task<IReadOnlyList<TaskOccurrence>> GetCandidateOccurrencesAsync(
        DoItTask task,
        DateTime fromUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var schedule = task.Schedule!;
        var timeZone = TimeZoneHelper.Find(schedule.TimeZoneId);
        var localFrom = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone).Date);
        var localTo = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            nowUtc.AddMinutes(MaximumEndOffsetMinutes),
            timeZone).Date);
        var storedFrom = localFrom.AddDays(-1);
        var storedTo = localTo.AddDays(1);
        var occurrences = await dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.TaskId == task.Id && occurrence.Date >= storedFrom && occurrence.Date <= storedTo)
            .ToListAsync(cancellationToken);
        var byDate = occurrences.ToDictionary(occurrence => occurrence.Date);

        if (schedule.RecurrenceType != RecurrenceType.TimesPerWeek)
        {
            var expectedDates = schedule.RecurrenceType == RecurrenceType.Manual
                ? schedule.StartDate >= storedFrom && schedule.StartDate <= storedTo ? [schedule.StartDate] : []
                : RecurrenceRules.GetExpectedDates(schedule, storedFrom, storedTo);
            foreach (var date in expectedDates)
            {
                if (byDate.ContainsKey(date))
                {
                    continue;
                }

                var occurrence = await occurrenceService.GetOrCreateAsync(task, date, nowUtc, cancellationToken);
                byDate[date] = occurrence;
            }
        }

        return byDate.Values.ToList();
    }

    private static void QueueScheduledDeliveries(
        DoItTask task,
        TaskOccurrence occurrence,
        Guid recipientId,
        IReadOnlyList<PushSubscription> subscriptions,
        UserNotificationPreference preference,
        DateTime fromUtc,
        DateTime nowUtc,
        ISet<string> existingKeys,
        ICollection<NotificationDelivery> newDeliveries)
    {
        var recommendedEnabled = IsEnabled(
            task.NotificationOverride?.RecommendedEnabled,
            preference.RecommendedEnabled);
        var recommendedDueAtUtc = recommendedEnabled ? occurrence.RecommendedAt : null;

        // Prefer the specific recommended-time notification when two task moments coincide.
        QueueScheduledEvent(
            task.NotificationOverride?.RecommendedEnabled,
            preference.RecommendedEnabled,
            occurrence.RecommendedAt,
            RecommendedSourceType,
            occurrence,
            recipientId,
            subscriptions,
            fromUtc,
            nowUtc,
            existingKeys,
            newDeliveries);
        QueueScheduledEvent(
            task.NotificationOverride?.AvailableFromEnabled,
            preference.AvailableFromEnabled,
            occurrence.AvailableFromAt,
            AvailableFromSourceType,
            occurrence,
            recipientId,
            subscriptions,
            fromUtc,
            nowUtc,
            existingKeys,
            newDeliveries,
            recommendedDueAtUtc);

        var endOffsetMinutes = task.NotificationOverride?.BeforeAvailableUntilMinutes ?? preference.BeforeAvailableUntilMinutes;
        var endDueAtUtc = IsValidEndOffset(endOffsetMinutes)
            ? occurrence.AvailableUntilAt?.AddMinutes(-endOffsetMinutes)
            : null;
        QueueScheduledEvent(
            task.NotificationOverride?.BeforeAvailableUntilEnabled,
            preference.BeforeAvailableUntilEnabled,
            endDueAtUtc,
            BeforeAvailableUntilSourceType,
            occurrence,
            recipientId,
            subscriptions,
            fromUtc,
            nowUtc,
            existingKeys,
            newDeliveries,
            recommendedDueAtUtc);
        QueueScheduledEvent(
            task.NotificationOverride?.TaskExpiredEnabled,
            preference.TaskExpiredEnabled,
            occurrence.AvailableUntilAt,
            ExpiredSourceType,
            occurrence,
            recipientId,
            subscriptions,
            fromUtc,
            nowUtc,
            existingKeys,
            newDeliveries,
            recommendedDueAtUtc);
    }

    private static void QueueScheduledEvent(
        bool? overrideEnabled,
        bool preferenceEnabled,
        DateTime? dueAtUtc,
        string sourceType,
        TaskOccurrence occurrence,
        Guid recipientId,
        IReadOnlyList<PushSubscription> subscriptions,
        DateTime fromUtc,
        DateTime nowUtc,
        ISet<string> existingKeys,
        ICollection<NotificationDelivery> newDeliveries,
        DateTime? skipIfDueAtUtc = null)
    {
        if (!IsEnabled(overrideEnabled, preferenceEnabled)
            || dueAtUtc is null
            || dueAtUtc == skipIfDueAtUtc
            || !IsValidDueAt(dueAtUtc.Value, fromUtc, nowUtc))
        {
            return;
        }

        var normalizedDueAtUtc = DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc);
        foreach (var subscription in subscriptions)
        {
            var deduplicationKey = BuildDeduplicationKey(
                sourceType,
                occurrence.Id,
                occurrence.Id,
                recipientId,
                subscription.Id,
                normalizedDueAtUtc);
            AddDeliveryIfMissing(
                newDeliveries,
                existingKeys,
                subscription,
                recipientId,
                sourceType,
                occurrence.Id,
                deduplicationKey,
                normalizedDueAtUtc,
                nowUtc);
        }
    }

    private async Task<Dictionary<Guid, UserNotificationPreference>> LoadPreferencesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        var stored = await dbContext.UserNotificationPreferences
            .AsNoTracking()
            .Where(preference => ids.Contains(preference.UserId))
            .ToDictionaryAsync(preference => preference.UserId, cancellationToken);
        foreach (var userId in ids)
        {
            stored.TryAdd(userId, new UserNotificationPreference { UserId = userId });
        }

        return stored;
    }

    private async Task<UserNotificationPreference> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken)
            ?? new UserNotificationPreference { UserId = userId };
    }

    private async Task<HashSet<string>> LoadExistingTaskDeliveryKeysAsync(
        IEnumerable<Guid> subscriptionIds,
        CancellationToken cancellationToken)
    {
        var ids = subscriptionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var deliveries = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(delivery => ids.Contains(delivery.PushSubscriptionId)
                && (delivery.SourceType == AvailableFromSourceType
                    || delivery.SourceType == RecommendedSourceType
                    || delivery.SourceType == BeforeAvailableUntilSourceType
                    || delivery.SourceType == ExpiredSourceType
                    || delivery.SourceType == CompletedSourceType))
            .Select(delivery => new { delivery.PushSubscriptionId, delivery.DeduplicationKey })
            .ToListAsync(cancellationToken);
        return deliveries
            .Select(delivery => BuildExistingKey(delivery.PushSubscriptionId, delivery.DeduplicationKey))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task SaveNewDeliveriesAsync(
        IReadOnlyCollection<NotificationDelivery> newDeliveries,
        CancellationToken cancellationToken)
    {
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

            logger.LogDebug(exception, "A task Web Push delivery was inserted concurrently; the unique deduplication index will keep one delivery.");
        }
    }

    private static void AddDeliveryIfMissing(
        ICollection<NotificationDelivery> newDeliveries,
        ISet<string> existingKeys,
        PushSubscription subscription,
        Guid userId,
        string sourceType,
        Guid sourceId,
        string deduplicationKey,
        DateTime dueAtUtc,
        DateTime now)
    {
        var existingKey = BuildExistingKey(subscription.Id, deduplicationKey);
        if (!existingKeys.Add(existingKey))
        {
            return;
        }

        newDeliveries.Add(new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PushSubscriptionId = subscription.Id,
            SourceType = sourceType,
            SourceId = sourceId,
            DeduplicationKey = deduplicationKey,
            DueAtUtc = DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc),
            Status = NotificationDeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static IReadOnlyList<Guid> ResolveTaskRecipients(DoItTask task, IReadOnlyCollection<Guid> activeUserIds)
    {
        if (task.Scope == TaskScope.Personal)
        {
            return activeUserIds.Contains(task.CreatedByUserId) ? [task.CreatedByUserId] : [];
        }

        if (task.AssignmentMode == AssignmentMode.Anyone || task.Assignments.Count == 0)
        {
            return activeUserIds.ToList();
        }

        var active = activeUserIds.ToHashSet();
        return task.Assignments
            .Select(assignment => assignment.UserId)
            .Where(active.Contains)
            .Distinct()
            .ToList();
    }

    private static bool IsTaskRecipient(DoItTask task, Guid userId)
    {
        if (task.Scope == TaskScope.Personal)
        {
            return task.CreatedByUserId == userId;
        }

        return task.AssignmentMode == AssignmentMode.Anyone
            || task.Assignments.Count == 0
            || task.Assignments.Any(assignment => assignment.UserId == userId);
    }

    private static bool IsCompletedRecipient(DoItTask task, Guid actorUserId, Guid userId)
    {
        if (actorUserId == userId)
        {
            return false;
        }

        if (task.Scope == TaskScope.House)
        {
            return true;
        }

        return task.CreatedByUserId == userId;
    }

    private async Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
    }

    private bool CanQueueNotifications() => _settings.Enabled && _settings.HasVapidConfiguration;

    private static bool IsEnabled(bool? overrideValue, bool preferenceValue) => overrideValue ?? preferenceValue;

    private static bool IsValidEndOffset(int minutes) => minutes is >= MinimumEndOffsetMinutes and <= MaximumEndOffsetMinutes;

    private static bool IsValidDueAt(DateTime dueAtUtc, DateTime fromUtc, DateTime nowUtc) =>
        dueAtUtc >= fromUtc && dueAtUtc <= nowUtc;

    private static string BuildExistingKey(Guid subscriptionId, string deduplicationKey) =>
        $"{subscriptionId:N}:{deduplicationKey}";

    private static string BuildDeduplicationKey(
        string sourceType,
        Guid occurrenceId,
        Guid sourceId,
        Guid userId,
        Guid subscriptionId,
        DateTime dueAtUtc) =>
        $"task:{sourceType}:{occurrenceId:N}:{sourceId:N}:{userId:N}:{subscriptionId:N}:{dueAtUtc.Ticks}";

    private static string BuildPayload(TaskDeliveryContext context)
    {
        var task = context.Task;
        var occurrence = context.Occurrence;
        var (title, body) = context.Delivery.SourceType switch
        {
            AvailableFromSourceType => ($"Ya disponible: {task.Title}", "La tarea ha entrado en su horario disponible."),
            RecommendedSourceType => ($"Hora recomendada: {task.Title}", "Es un buen momento para hacer esta tarea."),
            BeforeAvailableUntilSourceType => ($"Próximo fin: {task.Title}", $"La disponibilidad termina en {context.EndOffsetMinutes} minutos."),
            ExpiredSourceType => ($"Tarea vencida: {task.Title}", "La ventana de disponibilidad ha terminado."),
            CompletedSourceType => ($"Tarea completada: {task.Title}", "La tarea se ha completado."),
            _ => ($"Tarea: {task.Title}", "Tienes una actualización de una tarea.")
        };
        var tag = context.Delivery.DeduplicationKey;
        var payload = new
        {
            title,
            body,
            url = "/now",
            tag,
            data = new
            {
                sourceType = context.Delivery.SourceType,
                sourceId = context.Delivery.SourceId,
                taskId = task.Id,
                occurrenceId = occurrence.Id,
                occurrenceDate = occurrence.Date,
                dueAtUtc = DateTime.SpecifyKind(context.Delivery.DueAtUtc, DateTimeKind.Utc),
                completedByUserId = context.Completion?.UserId
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private sealed record TaskDeliveryContext(
        NotificationDelivery Delivery,
        DoItTask Task,
        TaskOccurrence Occurrence,
        TaskCompletion? Completion,
        int? EndOffsetMinutes);
}
