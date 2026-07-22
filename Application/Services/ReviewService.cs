using DoIt.Api.Application.Interfaces;
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
        var xpByCompletion = await dbContext.XpEvents
            .Where(xpEvent => xpEvent.UserId == userId && xpEvent.RevertedAt == null)
            .ToDictionaryAsync(xpEvent => xpEvent.CompletionId, xpEvent => xpEvent.Amount, cancellationToken);

        var completed = visible
            .Where(occurrence => ActiveCompletion(occurrence)?.Action == TaskCompletionAction.Done && IsDate(ActiveCompletion(occurrence)!.CreatedAt, date))
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var notApplicable = visible
            .Where(occurrence => occurrence.Date == date && ActiveCompletion(occurrence)?.Action == TaskCompletionAction.NotApplicable)
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notDone = visible
            .Where(occurrence => occurrence.Date == date && IsNotDoneForReview(occurrence, date, today))
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var futurePending = visible
            .Where(occurrence => occurrence.Date == date && date > today && occurrence.Status == OccurrenceStatus.Pending && ActiveCompletion(occurrence) is null)
            .Select(occurrence => ToItem(occurrence, xpByCompletion))
            .ToList();
        var created = new List<ReviewTaskResponse>();
        foreach (var task in visibleTasks.Where(task => IsDate(task.CreatedAt, date) && task.Schedule is not null && RecurrenceRules.AppliesOnDate(task.Schedule, date)))
        {
            var occurrence = visible.FirstOrDefault(candidate => candidate.TaskId == task.Id && candidate.Date == task.Schedule?.StartDate)
                ?? await occurrenceService.GetOrCreateAsync(task, date, DateTime.UtcNow, cancellationToken);
            created.Add(ToCreatedItem(task, occurrence));
        }

        var reviewOccurrences = visible.Where(occurrence => occurrence.Date == date).ToList();
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

    private static bool IsDate(DateTime value, DateOnly date) => DateOnly.FromDateTime(value) == date;

    private static bool IsNotDoneForReview(TaskOccurrence occurrence, DateOnly date, DateOnly today)
    {
        var activeCompletion = ActiveCompletion(occurrence);
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
