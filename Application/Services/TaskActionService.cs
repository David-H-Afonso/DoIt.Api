using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class TaskActionService(DoItDbContext dbContext, IXpService xpService) : ITaskActionService
{
    public Task<OccurrenceActionResponse> CompleteAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.Done, cancellationToken);
    }

    public Task<OccurrenceActionResponse> MissAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.Missed, cancellationToken);
    }

    public Task<OccurrenceActionResponse> NotApplicableAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return ApplyActionAsync(userId, occurrenceId, TaskCompletionAction.NotApplicable, cancellationToken);
    }

    public async Task<OccurrenceActionResponse> UndoAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        var occurrence = await GetUserOccurrenceAsync(userId, occurrenceId, cancellationToken);
        var completion = await dbContext.TaskCompletions
            .Where(candidate => candidate.OccurrenceId == occurrenceId && candidate.UserId == userId && candidate.RevertedAt == null)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (completion is null)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "nothing_to_undo", "There is no active action to undo.");
        }

        var now = DateTime.UtcNow;
        completion.RevertedAt = now;
        var userXp = await xpService.RevertCompletionAsync(completion, cancellationToken);
        occurrence.Status = OccurrenceStatus.Pending;
        occurrence.UpdatedAt = now;
        if (occurrence.Task?.Schedule?.RecurrenceType == RecurrenceType.Manual)
        {
            occurrence.Task.IsArchived = false;
            occurrence.Task.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(occurrence, 0, userXp);
    }

    private async Task<OccurrenceActionResponse> ApplyActionAsync(Guid userId, Guid occurrenceId, TaskCompletionAction action, CancellationToken cancellationToken)
    {
        var occurrence = await GetUserOccurrenceAsync(userId, occurrenceId, cancellationToken);
        if (action == TaskCompletionAction.Done && occurrence.AvailableFromAt is not null && DateTime.UtcNow < occurrence.AvailableFromAt)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "occurrence_unavailable", "Occurrence is not available yet.");
        }

        if (action == TaskCompletionAction.Done && occurrence.Task?.AssignmentMode == AssignmentMode.AllAssignees)
        {
            return await ApplyAllAssigneesDoneAsync(userId, occurrence, cancellationToken);
        }

        var status = ToStatus(action);
        if (occurrence.Status == status)
        {
            return ToResponse(occurrence);
        }

        var now = DateTime.UtcNow;
        occurrence.Status = status;
        occurrence.UpdatedAt = now;
        var completion = new TaskCompletion
        {
            Id = Guid.NewGuid(),
            OccurrenceId = occurrence.Id,
            UserId = userId,
            Action = action,
            CreatedAt = now
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

        return ToResponse(occurrence, xp.Item1, xp.Item2);
    }

    private async Task<OccurrenceActionResponse> ApplyAllAssigneesDoneAsync(Guid userId, TaskOccurrence occurrence, CancellationToken cancellationToken)
    {
        var hasActiveDone = await dbContext.TaskCompletions.AnyAsync(completion =>
            completion.OccurrenceId == occurrence.Id &&
            completion.UserId == userId &&
            completion.Action == TaskCompletionAction.Done &&
            completion.RevertedAt == null,
            cancellationToken);

        var now = DateTime.UtcNow;
        if (!hasActiveDone)
        {
            var completion = new TaskCompletion
            {
                Id = Guid.NewGuid(),
                OccurrenceId = occurrence.Id,
                UserId = userId,
                Action = TaskCompletionAction.Done,
                CreatedAt = now
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
        return ToResponse(occurrence);
    }

    private async Task<TaskOccurrence> GetUserOccurrenceAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken)
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

        var isAdmin = await dbContext.Users.AnyAsync(user => user.Id == userId && user.Role == UserRole.Admin, cancellationToken);
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

    private static OccurrenceActionResponse ToResponse(TaskOccurrence occurrence, int xpEarned = 0, UserXpResponse? userXp = null)
    {
        return new OccurrenceActionResponse(occurrence.Id, occurrence.TaskId, occurrence.Date, occurrence.Status.ToString(), xpEarned, userXp);
    }
}
