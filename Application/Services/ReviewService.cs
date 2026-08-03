using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class ReviewService(DoItDbContext dbContext, IOccurrenceService occurrenceService) : IReviewService
{
    private const string GeneralZoneName = "General";

    public async Task<ReviewResponse> GetReviewAsync(Guid userId, DateOnly date, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstAsync(candidate => candidate.Id == userId, cancellationToken);
        var isAdmin = user.Role == UserRole.Admin;
        var tasks = await dbContext.Tasks
            .Include(task => task.Zone)
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .ToListAsync(cancellationToken);
        var occurrences = await dbContext.TaskOccurrences
            .Include(occurrence => occurrence.Task)
            .ThenInclude(task => task!.Zone)
            .Include(occurrence => occurrence.Task)
            .ThenInclude(task => task!.Assignments)
            .Include(occurrence => occurrence.Completions)
            .ThenInclude(completion => completion.User)
            .ToListAsync(cancellationToken);

        var visibleTasks = tasks.Where(task => CanSee(task, userId, isAdmin)).ToList();
        var visible = occurrences.Where(occurrence => occurrence.Task != null && CanSee(occurrence.Task, userId, isAdmin)).ToList();
        var reviewOccurrences = visible.Where(occurrence => occurrence.Date == date).ToList();
        foreach (var task in visibleTasks)
        {
            var effectiveDate = GetReviewOccurrenceDate(task, date);
            if (effectiveDate is null)
            {
                continue;
            }

            var occurrence = visible.FirstOrDefault(candidate => candidate.TaskId == task.Id && candidate.Date == effectiveDate.Value)
                ?? await occurrenceService.GetOrCreateAsync(task, effectiveDate.Value, DateTime.UtcNow, cancellationToken);
            occurrence.Task ??= task;
            if (visible.All(candidate => candidate.Id != occurrence.Id))
            {
                visible.Add(occurrence);
            }

            var activeCompletion = ActiveCompletion(occurrence);
            var wasActionedOnDate = activeCompletion is not null
                && IsDate(activeCompletion.CreatedAt, date, occurrence.TimeZoneId ?? task.Schedule?.TimeZoneId);
            var isCarriedPending = effectiveDate.Value < date && occurrence.Status == OccurrenceStatus.Pending;
            if ((effectiveDate.Value == date || isCarriedPending || wasActionedOnDate)
                && reviewOccurrences.All(candidate => candidate.Id != occurrence.Id))
            {
                reviewOccurrences.Add(occurrence);
            }
        }

        var xpByCompletion = await dbContext.XpEvents
            .Where(xpEvent => xpEvent.UserId == userId && xpEvent.RevertedAt == null)
            .ToDictionaryAsync(xpEvent => xpEvent.CompletionId, xpEvent => xpEvent.Amount, cancellationToken);

        var completed = visible
            .Where(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.Done
                && IsDate(ActiveCompletion(occurrence)!.CreatedAt, date, occurrence.TimeZoneId ?? occurrence.Task?.Schedule?.TimeZoneId))
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var notApplicable = visible
            .Where(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.NotApplicable
                && IsDate(ActiveCompletion(occurrence)!.CreatedAt, date, occurrence.TimeZoneId ?? occurrence.Task?.Schedule?.TimeZoneId))
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notDone = reviewOccurrences
            .Where(occurrence => IsNotDoneForReview(occurrence, date, today))
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var futurePending = reviewOccurrences
            .Where(occurrence => occurrence.Date == date && date > today && occurrence.Status == OccurrenceStatus.Pending && ActiveCompletion(occurrence) is null)
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var created = new List<ReviewTaskResponse>();
        foreach (var task in visibleTasks.Where(task => IsDate(task.CreatedAt, date) && task.Schedule is not null && RecurrenceRules.AppliesOnDate(task.Schedule, date)))
        {
            var occurrence = reviewOccurrences.FirstOrDefault(candidate => candidate.TaskId == task.Id && candidate.Date == task.Schedule?.StartDate)
                ?? visible.FirstOrDefault(candidate => candidate.TaskId == task.Id && candidate.Date == date)
                ?? await occurrenceService.GetOrCreateAsync(task, date, DateTime.UtcNow, cancellationToken);
            occurrence.Task ??= task;
            created.Add(ToCreatedItem(task, occurrence));
        }

        var byZone = reviewOccurrences
            .GroupBy(occurrence => new { occurrence.Task!.ZoneId, ZoneName = occurrence.Task.Zone?.Name ?? GeneralZoneName })
            .Select(group => new ReviewZoneResponse(
                group.Key.ZoneId,
                group.Key.ZoneName,
                group.Count(),
            group.Count(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.Done),
            group.Count(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.Missed),
            group.Count(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.NotApplicable),
            group.Count(occurrence => ActiveCompletion(occurrence) is null)))
            .OrderBy(zone => zone.ZoneName)
            .ToList();

        return new ReviewResponse(
            date,
            completed.Sum(item => item.XpEarned),
            completed,
            notDone,
            notApplicable,
            futurePending,
            byZone,
            created);
    }

    private static ReviewTaskResponse ToItem(TaskOccurrence occurrence, IReadOnlyDictionary<Guid, int> xpByCompletion)
    {
        var activeCompletion = ActiveCompletion(occurrence);
        var xp = activeCompletion is not null && xpByCompletion.TryGetValue(activeCompletion.Id, out var amount) ? amount : 0;
        return new ReviewTaskResponse(
            occurrence.Id,
            occurrence.TaskId,
            occurrence.Task!.Title,
            occurrence.Task.Zone?.Name,
            occurrence.Status.ToString(),
            activeCompletion?.Action == TaskCompletionAction.Done ? activeCompletion.User?.DisplayName : null,
            activeCompletion?.Action == TaskCompletionAction.Done ? xp : 0,
            occurrence.Task.CreatedAt,
            activeCompletion?.Action == TaskCompletionAction.Done ? activeCompletion.CreatedAt : null);
    }

    private static ReviewTaskResponse ToCreatedItem(DoItTask task, TaskOccurrence occurrence)
    {
        return new ReviewTaskResponse(occurrence.Id, task.Id, task.Title, task.Zone?.Name, "Created", null, 0, task.CreatedAt, null);
    }

    private static TaskCompletion? ActiveCompletion(TaskOccurrence occurrence)
    {
        return occurrence.Completions
            .Where(completion => completion.RevertedAt == null)
            .OrderByDescending(completion => completion.CreatedAt)
            .FirstOrDefault();
    }

    private static bool IsDate(DateTime value, DateOnly date, string? timeZoneId = null)
    {
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZoneHelper.Find(timeZoneId)).Date) == date;
    }

    private static DateOnly? GetReviewOccurrenceDate(DoItTask task, DateOnly date)
    {
        var schedule = task.Schedule;
        if (schedule is null || DateOnly.FromDateTime(task.CreatedAt) > date)
        {
            return null;
        }

        if (schedule.RecurrenceType == RecurrenceType.Manual)
        {
            return schedule.StartDate == date ? date : null;
        }

        if (schedule.RecurrenceType == RecurrenceType.TimesPerWeek)
        {
            return null;
        }

        return RecurrenceRules.GetEffectiveOccurrenceDate(schedule, date);
    }

    private static bool IsNotDoneForReview(TaskOccurrence occurrence, DateOnly date, DateOnly today)
    {
        var activeCompletion = ActiveCompletion(occurrence);
        if (occurrence.Task?.Schedule is { } schedule && RecurrenceRules.IsExpiredExtendedOccurrence(schedule, occurrence.Date, today))
        {
            return false;
        }

        if (occurrence.Task?.Schedule?.RecurrenceType == RecurrenceType.Manual && occurrence.Task.Schedule.AvailableUntilTime is null)
        {
            return false;
        }
        var isExplicitMiss = activeCompletion?.Action == TaskCompletionAction.Missed;
        if (occurrence.Task?.Schedule?.RecurrenceType == RecurrenceType.TimesPerWeek)
        {
            return (date < today || isExplicitMiss) && (occurrence.Status == OccurrenceStatus.Missed || isExplicitMiss);
        }

        return (date < today || isExplicitMiss) && (activeCompletion is null || isExplicitMiss);
    }

    private static bool CanSee(DoItTask task, Guid userId, bool isAdmin)
    {
        if (task.Scope == TaskScope.Personal)
        {
            return task.CreatedByUserId == userId;
        }

        return isAdmin || task.AssignmentMode == AssignmentMode.Anyone || task.Assignments.Any(assignment => assignment.UserId == userId);
    }
}
