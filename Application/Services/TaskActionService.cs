using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class TaskActionService(
    DoItDbContext dbContext,
    IXpService xpService,
    ITaskNotificationService taskNotificationService) : ITaskActionService
{
    public Task<OccurrenceActionResponse> CompleteAsync(Guid userId, Guid occurrenceId, bool allowAdminOverride, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.Done, allowEarly: false, allowAdminOverride, cancellationToken);
    }

    public async Task<OccurrenceActionResponse> CompleteRetroactivelyAsync(Guid userId, Guid occurrenceId, DateOnly date, CancellationToken cancellationToken)
    {
        var occurrence = await GetUserOccurrenceAsync(userId, occurrenceId, allowAdminOverride: true, cancellationToken);
        var timeZone = TimeZoneHelper.Find(occurrence.TimeZoneId ?? occurrence.Task?.Schedule?.TimeZoneId);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date);
        if (date >= localToday)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "retroactive_date_not_past", "A retroactive completion must belong to a past date.");
        }

        if (date < occurrence.Date)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "retroactive_date_before_occurrence", "The completion date cannot be before the occurrence.");
        }

        var schedule = occurrence.Task?.Schedule;
        if (schedule?.RecurrenceType == RecurrenceType.TimesPerWeek)
        {
            if (date != occurrence.Date)
            {
                throw new ApiException(StatusCodes.Status409Conflict, "retroactive_date_not_occurrence", "The completion date does not belong to this occurrence.");
            }
        }
        else if (schedule is not null && RecurrenceRules.GetEffectiveOccurrenceDate(schedule, date) != occurrence.Date)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "retroactive_date_not_occurrence", "The completion date does not belong to this occurrence.");
        }

        var completedAt = GetRetroactiveCompletionAt(schedule, occurrence, date);
        return await ApplyActionAsync(
            userId,
            occurrenceId,
            TaskCompletionAction.Done,
            allowEarly: false,
            allowAdminOverride: true,
            cancellationToken,
            completedAt);
    }

    public Task<OccurrenceActionResponse> CompleteEarlyAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.Done, allowEarly: true, allowAdminOverride: true, cancellationToken);
    }

    public Task<OccurrenceActionResponse> MissAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.Missed, allowEarly: false, allowAdminOverride: true, cancellationToken);
    }

    public Task<OccurrenceActionResponse> NotApplicableAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.NotApplicable, allowEarly: false, allowAdminOverride: true, cancellationToken);
    }

    public async Task<OccurrenceActionResponse> UndoAsync(Guid userId, Guid occurrenceId, bool allowAdminOverride, CancellationToken cancellationToken)
    {
        var occurrence = await GetUserOccurrenceAsync(userId, occurrenceId, allowAdminOverride, cancellationToken);
        var isAdmin = allowAdminOverride && await dbContext.Users.AnyAsync(user => user.Id == userId && user.Role == UserRole.Admin, cancellationToken);
        var canUndoAnyHouseAction = isAdmin && occurrence.Task!.Scope == TaskScope.House;
        var completionQuery = dbContext.TaskCompletions
            .Where(candidate => candidate.OccurrenceId == occurrenceId && candidate.RevertedAt == null);
        if (!canUndoAnyHouseAction)
        {
            completionQuery = completionQuery.Where(candidate => candidate.UserId == userId);
        }

        var completion = await completionQuery
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (completion is null)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "nothing_to_undo", "There is no active action to undo.");
        }

        var now = DateTime.UtcNow;
        completion.RevertedAt = now;
        var userXp = await xpService.RevertCompletionAsync(completion, cancellationToken);
        occurrence.Status = await RecalculateStatusAsync(occurrence, completion.Id, cancellationToken);
        occurrence.UpdatedAt = now;
        if (occurrence.Task?.Schedule?.RecurrenceType == RecurrenceType.Manual)
        {
            occurrence.Task.IsArchived = false;
            occurrence.Task.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(occurrence, 0, userXp);
    }

    private async Task<OccurrenceStatus> RecalculateStatusAsync(TaskOccurrence occurrence, Guid revertedCompletionId, CancellationToken cancellationToken)
    {
        var activeCompletions = await dbContext.TaskCompletions
            .Where(completion => completion.OccurrenceId == occurrence.Id && completion.Id != revertedCompletionId && completion.RevertedAt == null)
            .OrderBy(completion => completion.CreatedAt)
            .ToListAsync(cancellationToken);
        if (activeCompletions.Count == 0)
        {
            return OccurrenceStatus.Pending;
        }

        if (occurrence.Task?.AssignmentMode == AssignmentMode.AllAssignees)
        {
            var assigneeIds = occurrence.Task.Assignments.Select(assignment => assignment.UserId).Distinct().ToList();
            var doneUserIds = activeCompletions
                .Where(completion => completion.Action == TaskCompletionAction.Done)
                .Select(completion => completion.UserId)
                .Distinct()
                .ToHashSet();
            return assigneeIds.Count > 0 && assigneeIds.All(doneUserIds.Contains)
                ? OccurrenceStatus.Done
                : OccurrenceStatus.Pending;
        }

        var latest = activeCompletions[^1];
        if (latest.Action == TaskCompletionAction.NotApplicable && activeCompletions.Any(completion => completion.Action == TaskCompletionAction.Done))
        {
            return OccurrenceStatus.Done;
        }

        return ToStatus(latest.Action);
    }

    private async Task<OccurrenceActionResponse> ApplyActionAsync(
        Guid userId,
        Guid occurrenceId,
        TaskCompletionAction action,
        bool allowEarly,
        bool allowAdminOverride,
        CancellationToken cancellationToken,
        DateTime? completionCreatedAt = null)
    {
        var occurrence = await GetUserOccurrenceAsync(userId, occurrenceId, allowAdminOverride, cancellationToken);
        if (action == TaskCompletionAction.Done && allowEarly)
        {
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneHelper.Find(occurrence.TimeZoneId ?? occurrence.Task?.Schedule?.TimeZoneId)).Date);
            if (occurrence.Date <= localToday)
            {
                throw new ApiException(StatusCodes.Status409Conflict, "occurrence_not_future", "Only a future occurrence can be completed early.");
            }
        }


        if (action == TaskCompletionAction.Done && !allowEarly)
        {
            var schedule = occurrence.Task?.Schedule;
            var timeZone = TimeZoneHelper.Find(schedule?.TimeZoneId ?? occurrence.TimeZoneId);
            var currentUtc = DateTime.UtcNow;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(currentUtc, timeZone).Date);
            DateTime? availableFromAt = schedule?.AvailableFromTime is { } availableFrom
                ? TimeZoneHelper.ToUtc(occurrence.Date, availableFrom, schedule.TimeZoneId)
                : null;
            if (occurrence.Date > localToday || availableFromAt is not null && currentUtc < availableFromAt)
            {
                throw new ApiException(StatusCodes.Status409Conflict, "occurrence_unavailable", "Occurrence is not available yet.");
            }
        }

        if (action == TaskCompletionAction.Done && occurrence.Task?.AssignmentMode == AssignmentMode.AllAssignees)
        {
            return await ApplyAllAssigneesDoneAsync(userId, occurrence, cancellationToken, completionCreatedAt);
        }

        var status = ToStatus(action);
        var hasActiveDone = action == TaskCompletionAction.NotApplicable && await dbContext.TaskCompletions.AnyAsync(completion =>
            completion.OccurrenceId == occurrence.Id &&
            completion.Action == TaskCompletionAction.Done &&
            completion.RevertedAt == null,
            cancellationToken);
        if (occurrence.Status == status)
        {
            return ToResponse(occurrence);
        }

        var now = DateTime.UtcNow;
        occurrence.Status = hasActiveDone ? OccurrenceStatus.Done : status;
        occurrence.UpdatedAt = now;
        var completion = new TaskCompletion
        {
            Id = Guid.NewGuid(),
            OccurrenceId = occurrence.Id,
            UserId = userId,
            Action = action,
            CreatedAt = completionCreatedAt ?? now
        };
        dbContext.TaskCompletions.Add(completion);

        var xp = action == TaskCompletionAction.Done ? await xpService.AwardCompletionAsync(occurrence, completion, cancellationToken) : (0, null);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (action == TaskCompletionAction.Done && occurrence.Task?.Schedule?.RecurrenceType == RecurrenceType.Manual)
        {
            occurrence.Task.IsArchived = true;
            occurrence.Task.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (action == TaskCompletionAction.Done)
        {
            await taskNotificationService.QueueTaskCompletedAsync(occurrence, completion, cancellationToken);
        }

        return ToResponse(occurrence, xp.Item1, xp.Item2);
    }

    private async Task<OccurrenceActionResponse> ApplyAllAssigneesDoneAsync(Guid userId, TaskOccurrence occurrence, CancellationToken cancellationToken, DateTime? completionCreatedAt = null)
    {
        var hasActiveDone = await dbContext.TaskCompletions.AnyAsync(completion =>
            completion.OccurrenceId == occurrence.Id &&
            completion.UserId == userId &&
            completion.Action == TaskCompletionAction.Done &&
            completion.RevertedAt == null,
            cancellationToken);

        var now = DateTime.UtcNow;
        TaskCompletion? completion = null;
        if (!hasActiveDone)
        {
            completion = new TaskCompletion
            {
                Id = Guid.NewGuid(),
                OccurrenceId = occurrence.Id,
                UserId = userId,
                Action = TaskCompletionAction.Done,
                CreatedAt = completionCreatedAt ?? now
            };
            dbContext.TaskCompletions.Add(completion);
            await xpService.AwardCompletionAsync(occurrence, completion, cancellationToken);
        }

        var assigneeIds = occurrence.Task?.Assignments.Select(assignment => assignment.UserId).Distinct().ToList() ?? [];
        var doneUserIds = await dbContext.TaskCompletions
            .Where(completion => completion.OccurrenceId == occurrence.Id && completion.Action == TaskCompletionAction.Done && completion.RevertedAt == null)
            .Select(completion => completion.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!doneUserIds.Contains(userId))
        {
            doneUserIds.Add(userId);
        }

        occurrence.Status = assigneeIds.Count > 0 && assigneeIds.All(doneUserIds.Contains) ? OccurrenceStatus.Done : OccurrenceStatus.Pending;
        occurrence.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (completion is not null && occurrence.Status == OccurrenceStatus.Done)
        {
            await taskNotificationService.QueueTaskCompletedAsync(occurrence, completion, cancellationToken);
        }

        return ToResponse(occurrence);
    }

    private async Task<TaskOccurrence> GetUserOccurrenceAsync(Guid userId, Guid occurrenceId, bool allowAdminOverride, CancellationToken cancellationToken)
    {
        var occurrence = await dbContext.TaskOccurrences
            .Include(candidate => candidate.Task)
            .ThenInclude(task => task!.Assignments)
            .Include(candidate => candidate.Task)
            .ThenInclude(task => task!.Schedule)
            .FirstOrDefaultAsync(candidate => candidate.Id == occurrenceId && candidate.Task != null, cancellationToken);

        if (occurrence is null || occurrence.Task is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "occurrence_not_found", "Occurrence not found.");
        }

        var isAdmin = allowAdminOverride && await dbContext.Users.AnyAsync(user => user.Id == userId && user.Role == UserRole.Admin, cancellationToken);
        if (!CanActOnTask(occurrence.Task, userId, isAdmin))
        {
            throw new ApiException(StatusCodes.Status404NotFound, "occurrence_not_found", "Occurrence not found.");
        }

        return occurrence;
    }

    private static bool CanActOnTask(DoItTask task, Guid userId, bool isAdmin)
    {
        if (task.Scope == TaskScope.Personal)
        {
            return task.CreatedByUserId == userId;
        }

        return isAdmin || task.AssignmentMode == AssignmentMode.Anyone || task.Assignments.Any(assignment => assignment.UserId == userId);
    }

    private static OccurrenceStatus ToStatus(TaskCompletionAction action)
    {
        return action switch
        {
            TaskCompletionAction.Done => OccurrenceStatus.Done,
            TaskCompletionAction.Missed => OccurrenceStatus.Missed,
            TaskCompletionAction.NotApplicable => OccurrenceStatus.NotApplicable,
            _ => OccurrenceStatus.Pending
        };
    }

    private static DateTime GetRetroactiveCompletionAt(TaskSchedule? schedule, TaskOccurrence occurrence, DateOnly date)
    {
        var localTime = schedule?.AvailableUntilTime is { } availableUntil
            ? availableUntil.AddMinutes(-1)
            : new TimeOnly(23, 59);
        var localDateTime = date.ToDateTime(localTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneHelper.Find(occurrence.TimeZoneId ?? schedule?.TimeZoneId));
    }

    private static OccurrenceActionResponse ToResponse(TaskOccurrence occurrence, int xpEarned = 0, UserXpResponse? userXp = null)
    {
        return new OccurrenceActionResponse(occurrence.Id, occurrence.TaskId, occurrence.Date, occurrence.Status.ToString(), xpEarned, userXp);
    }
}
