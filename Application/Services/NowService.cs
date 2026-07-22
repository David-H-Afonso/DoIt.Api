using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class NowService(DoItDbContext dbContext, IOccurrenceService occurrenceService) : INowService
{
    private const string GeneralZoneName = "General";

    public async Task<NowResponse> GetNowAsync(Guid userId, DateOnly? date, string? scope, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedScope = NormalizeScope(scope);
            var user = await dbContext.Users.FirstAsync(candidate => candidate.Id == userId, cancellationToken);

        var tasks = await dbContext.Tasks
            .Include(task => task.Zone)
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .ThenInclude(assignment => assignment.User)
            .Where(task => !task.IsArchived)
            .OrderBy(task => task.Zone == null ? int.MaxValue : task.Zone.SortOrder)
            .ThenBy(task => task.Zone == null ? GeneralZoneName : task.Zone.Name)
            .ThenBy(task => task.Title)
            .ToListAsync(cancellationToken);

        var visibleTasks = tasks.Where(task => MatchesScope(task, normalizedScope, userId, user.Role == UserRole.Admin)).ToList();
        var visibleItems = new List<NowItem>();
        var now = DateTime.UtcNow;
        foreach (var task in visibleTasks)
        {
            if (task.Schedule?.RecurrenceType == RecurrenceType.TimesPerWeek && await WeeklyTargetReachedAsync(task, targetDate, cancellationToken))
            {
                continue;
            }

            var classified = Classify(task, targetDate, GetCurrentTime(task.Schedule?.TimeZoneId, targetDate));
            if (classified is null)
            {
                continue;
            }

            var occurrenceDate = task.Schedule?.RecurrenceType == RecurrenceType.Manual && task.Schedule.StartDate < targetDate
                ? task.Schedule.StartDate
                : targetDate;
            var occurrence = await occurrenceService.GetOrCreateAsync(task, occurrenceDate, now, cancellationToken);
            if (normalizedScope == "me" && task.AssignmentMode == AssignmentMode.AllAssignees && await UserAlreadyCompletedAsync(userId, occurrence.Id, cancellationToken))
            {
                continue;
            }

            visibleItems.Add(classified with { Occurrence = occurrence });
        }

        var zones = visibleItems
            .GroupBy(item => new { item.Task.ZoneId, ZoneName = item.Task.Zone?.Name ?? GeneralZoneName, SortOrder = item.Task.Zone?.SortOrder ?? int.MaxValue })
            .OrderBy(group => group.Key.SortOrder)
            .ThenBy(group => group.Key.ZoneName)
            .Select(group => BuildZone(group.Key.ZoneId, group.Key.ZoneName, group))
            .ToList();

        var upcomingToday = SortTasks(visibleItems
            .Where(item => item.Status == "unavailable" && item.Occurrence.Status == OccurrenceStatus.Pending)
            .Select(item => ToTaskResponse(new NowItem(item.Task, item.Occurrence, "upcoming"))));
        return new NowResponse(targetDate, normalizedScope, BuildProgress(visibleItems.Select(item => item.Occurrence)), zones, upcomingToday);
    }

    private static NowZoneResponse BuildZone(Guid? zoneId, string zoneName, IEnumerable<NowItem> items)
    {
        var itemList = items.ToList();
        var pending = itemList.Where(item => item.Occurrence.Status == OccurrenceStatus.Pending).ToList();
        var overdue = SortTasks(itemList.Where(item => item.Status == "overdue" || IsWeeklyMissed(item)).Select(ToTaskResponse));
        var available = SortTasks(pending.Where(item => item.Status == "available").Select(ToTaskResponse));
        var unavailable = Array.Empty<NowTaskResponse>();
        var completed = itemList.Where(item => item.Occurrence.Status != OccurrenceStatus.Pending && !IsWeeklyMissed(item)).Select(ToTaskResponse).ToList();

        return new NowZoneResponse(zoneId, zoneName, BuildProgress(itemList.Select(item => item.Occurrence)), overdue, available, unavailable, completed);
    }

    private static IReadOnlyList<NowTaskResponse> SortTasks(IEnumerable<NowTaskResponse> tasks)
    {
        return tasks
            .OrderBy(task => task.RecurrenceType == "Manual" ? 2 : task.RecommendedTime is not null || task.AvailableFromTime is not null || task.AvailableUntilTime is not null ? 0 : 1)
            .ThenBy(task => task.Status is "upcoming" or "unavailable"
                ? task.AvailableFromTime ?? task.RecommendedTime ?? task.AvailableUntilTime ?? TimeOnly.MaxValue
                : task.RecommendedTime ?? task.AvailableFromTime ?? task.AvailableUntilTime ?? TimeOnly.MaxValue)
            .ThenBy(task => task.Title)
            .ToList();
    }

    private static NowTaskResponse ToTaskResponse(NowItem item)
    {
        var schedule = item.Task.Schedule;
        var completed = item.Occurrence.Completions
            .Where(completion => completion.RevertedAt is null && completion.Action == TaskCompletionAction.Done)
            .OrderByDescending(completion => completion.CreatedAt)
            .FirstOrDefault();
        var activeCompletion = item.Occurrence.Completions
            .Where(completion => completion.RevertedAt is null)
            .OrderByDescending(completion => completion.CreatedAt)
            .FirstOrDefault();
        DateTime? completedAt = completed is null ? null : DateTime.SpecifyKind(completed.CreatedAt, DateTimeKind.Utc);
        var completionTiming = completedAt is not null && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(completedAt.Value, TimeZoneHelper.Find(item.Occurrence.TimeZoneId ?? schedule?.TimeZoneId))).DayNumber < item.Occurrence.Date.DayNumber
            ? "Early"
            : null;
        return new NowTaskResponse(
            item.Occurrence.Id,
            item.Task.Id,
            item.Task.Title,
            item.Task.ZoneId,
            item.Task.Zone?.Name,
            item.Task.Scope.ToString(),
            item.Task.AssignmentMode.ToString(),
            item.Task.Assignments.Select(assignment => assignment.UserId).ToList(),
            item.Task.Assignments.Select(assignment => assignment.User?.DisplayName ?? string.Empty).Where(name => !string.IsNullOrWhiteSpace(name)).ToList(),
            item.Status,
            item.Occurrence.Status.ToString(),
            item.Occurrence.Date,
            completionTiming,
            completedAt,
            activeCompletion?.UserId,
            schedule?.AvailableFromTime,
            schedule?.AvailableUntilTime,
            schedule?.RecommendedTime,
            schedule?.TimeZoneId ?? "UTC",
            schedule?.RecurrenceType.ToString() ?? RecurrenceType.Manual.ToString());
    }

    private static NowProgressResponse BuildProgress(IEnumerable<TaskOccurrence> occurrences)
    {
        var occurrenceList = occurrences.ToList();
        var done = occurrenceList.Count(occurrence => occurrence.Status == OccurrenceStatus.Done);
        var missed = occurrenceList.Count(occurrence => occurrence.Status == OccurrenceStatus.Missed);
        var notApplicable = occurrenceList.Count(occurrence => occurrence.Status == OccurrenceStatus.NotApplicable);
        var pending = occurrenceList.Count(occurrence => occurrence.Status == OccurrenceStatus.Pending);
        return new NowProgressResponse(occurrenceList.Count, done, missed, notApplicable, pending);
    }

    private static NowItem? Classify(DoItTask task, DateOnly date, TimeOnly currentTime)
    {
        var schedule = task.Schedule;
        if (schedule is null || !AppliesOnDate(task, schedule, date))
        {
            return null;
        }

        if (schedule.RecurrenceType == RecurrenceType.Manual && schedule.StartDate < date)
        {
            return new NowItem(task, null!, "overdue");
        }

        if (schedule.AvailableFromTime is not null && currentTime < schedule.AvailableFromTime)
        {
            return schedule.UnavailableVisibilityMode == UnavailableVisibilityMode.Hidden ? null : new NowItem(task, null!, "unavailable");
        }

        if (schedule.AvailableUntilTime is not null && currentTime > schedule.AvailableUntilTime)
        {
            return new NowItem(task, null!, "overdue");
        }

        return new NowItem(task, null!, "available");
    }

    private static bool AppliesOnDate(DoItTask task, TaskSchedule schedule, DateOnly date)
    {
        if (date < schedule.StartDate || schedule.EndDate is not null && date > schedule.EndDate)
        {
            return false;
        }

        return schedule.RecurrenceType switch
        {
            RecurrenceType.Manual => date >= schedule.StartDate,
            RecurrenceType.Daily => true,
            RecurrenceType.Weekly => date.DayOfWeek == schedule.StartDate.DayOfWeek,
            RecurrenceType.Weekday => schedule.Weekday == date.DayOfWeek,
            RecurrenceType.TimesPerWeek => true,
            RecurrenceType.EveryNDays => schedule.EveryNDays is > 0 && (date.DayNumber - schedule.StartDate.DayNumber) % schedule.EveryNDays.Value == 0,
            RecurrenceType.MonthlyOrdinalWeekday => schedule.Weekday == date.DayOfWeek && ((date.Day - 1) / 7) + 1 == schedule.WeekOfMonth,
            _ => task.TaskType == TaskType.Routine
        };
    }

    private async Task<bool> WeeklyTargetReachedAsync(DoItTask task, DateOnly date, CancellationToken cancellationToken)
    {
        var schedule = task.Schedule;
        if (schedule?.TimesPerWeek is not > 0)
        {
            return false;
        }

        var weekStart = StartOfWeek(date);
        var weekEnd = StartOfWeek(date).AddDays(6);
        if (schedule.EndDate is not null)
        {
            weekEnd = schedule.EndDate.Value < weekEnd ? schedule.EndDate.Value : weekEnd;
        }

        var completed = await dbContext.TaskOccurrences
            .Where(occurrence => occurrence.TaskId == task.Id && occurrence.Date >= weekStart && occurrence.Date <= weekEnd && occurrence.Status == OccurrenceStatus.Done)
            .CountAsync(cancellationToken);
        return completed >= schedule.TimesPerWeek.Value;
    }

    private static bool IsWeeklyMissed(NowItem item) => item.Task.Schedule?.RecurrenceType == RecurrenceType.TimesPerWeek && item.Occurrence.Status == OccurrenceStatus.Missed;

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static TimeOnly GetCurrentTime(string? timeZoneId, DateOnly targetDate)
    {
        if (targetDate != DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return new TimeOnly(12, 0);
        }

        return TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneHelper.Find(timeZoneId)));
    }

    private async Task<bool> UserAlreadyCompletedAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return await dbContext.TaskCompletions.AnyAsync(completion =>
            completion.OccurrenceId == occurrenceId &&
            completion.UserId == userId &&
            completion.Action == TaskCompletionAction.Done &&
            completion.RevertedAt == null,
            cancellationToken);
    }

    private static bool MatchesScope(DoItTask task, string scope, Guid userId, bool isAdmin)
    {
        return scope switch
        {
            "house" => task.Scope == TaskScope.House,
            "all" => task.Scope == TaskScope.Personal && task.CreatedByUserId == userId || task.Scope == TaskScope.House,
            _ => task.Scope == TaskScope.Personal && task.CreatedByUserId == userId || task.Scope == TaskScope.House && CanSeeHouseTask(task, userId, isAdmin, includeAnyone: true)
        };
    }

    private static bool CanSeeHouseTask(DoItTask task, Guid userId, bool isAdmin, bool includeAnyone)
    {
        if (isAdmin)
        {
            return true;
        }

        return task.AssignmentMode == AssignmentMode.Anyone && includeAnyone || task.Assignments.Any(assignment => assignment.UserId == userId);
    }

    private static string NormalizeScope(string? scope)
    {
        return string.Equals(scope, "house", StringComparison.OrdinalIgnoreCase)
            ? "house"
            : string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)
                ? "all"
                : "me";
    }

    private sealed record NowItem(DoItTask Task, TaskOccurrence Occurrence, string Status);
}
