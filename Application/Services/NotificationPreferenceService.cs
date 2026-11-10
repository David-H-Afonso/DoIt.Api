using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class NotificationPreferenceService(DoItDbContext dbContext) : INotificationPreferenceService
{
    public async Task<NotificationPreferenceResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preference = await GetOrCreateAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(preference);
    }

    public async Task<NotificationPreferenceResponse> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var preference = await GetOrCreateAsync(userId, cancellationToken);
        preference.AvailableFromEnabled = request.AvailableFromEnabled ?? preference.AvailableFromEnabled;
        preference.RecommendedEnabled = request.RecommendedEnabled ?? preference.RecommendedEnabled;
        preference.BeforeAvailableUntilEnabled = request.BeforeAvailableUntilEnabled ?? preference.BeforeAvailableUntilEnabled;
        preference.TaskExpiredEnabled = request.TaskExpiredEnabled ?? preference.TaskExpiredEnabled;
        preference.TaskCompletedEnabled = request.TaskCompletedEnabled ?? preference.TaskCompletedEnabled;
        preference.BeforeAvailableUntilMinutes = NormalizeOffset(request.BeforeAvailableUntilMinutes ?? preference.BeforeAvailableUntilMinutes);
        preference.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(preference);
    }

    public async Task<TaskNotificationSettingsResponse> ListTaskSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userExists = await dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
        if (!userExists)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        // Listing must not create preferences or make a request per task. Keep the
        // same visibility rules as the existing individual notification endpoint.
        var preference = await dbContext.UserNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? new UserNotificationPreference { UserId = userId };
        var tasks = await dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.NotificationOverride)
            .Where(task => task.CreatedByUserId == userId
                || task.Scope == TaskScope.House && (task.AssignmentMode == AssignmentMode.Anyone
                    || task.Assignments.Any(assignment => assignment.UserId == userId)))
            .OrderBy(task => task.Id)
            .ToListAsync(cancellationToken);

        return new TaskNotificationSettingsResponse(
            ToResponse(preference),
            tasks.Select(task => ToResponse(task.Id, task.NotificationOverride)).ToList());
    }

    public async Task<IReadOnlyList<TaskNotificationOverrideResponse>> UpdateTaskOverridesAsync(
        Guid userId,
        UpdateTaskNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tasks is null || request.Tasks.Count is < 1 or > 500
            || request.Tasks.Any(update => update is null || update.TaskId == Guid.Empty || update.Settings is null))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_notification_batch", "Choose between one and 500 tasks.");
        }

        var taskIds = request.Tasks.Select(update => update.TaskId).Distinct().ToList();
        if (taskIds.Count != request.Tasks.Count)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_notification_batch", "Each task may appear only once.");
        }

        var ownedTaskCount = await dbContext.Tasks
            .CountAsync(task => taskIds.Contains(task.Id) && task.CreatedByUserId == userId, cancellationToken);
        if (ownedTaskCount != taskIds.Count)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        // Validate the entire batch before tracking changes: either every task is
        // updated in one SaveChanges transaction, or none is changed.
        foreach (var update in request.Tasks)
        {
            if (update.Settings.BeforeAvailableUntilMinutes is { } minutes)
            {
                NormalizeOffset(minutes);
            }
        }

        var stored = await dbContext.TaskNotificationOverrides
            .Where(candidate => taskIds.Contains(candidate.TaskId))
            .ToDictionaryAsync(candidate => candidate.TaskId, cancellationToken);
        var now = DateTime.UtcNow;
        var result = new List<TaskNotificationOverrideResponse>(request.Tasks.Count);
        foreach (var update in request.Tasks)
        {
            if (!stored.TryGetValue(update.TaskId, out var notificationOverride))
            {
                notificationOverride = new TaskNotificationOverride { TaskId = update.TaskId, CreatedAt = now };
                dbContext.TaskNotificationOverrides.Add(notificationOverride);
            }

            ApplyOverride(notificationOverride, update.Settings, now);
            result.Add(ToResponse(update.TaskId, notificationOverride));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<TaskNotificationOverrideResponse> GetTaskOverrideAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await GetTaskForUserAsync(userId, taskId, requireOwner: false, cancellationToken);
        var notificationOverride = await dbContext.TaskNotificationOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TaskId == taskId, cancellationToken);

        return ToResponse(taskId, notificationOverride);
    }

    public async Task<TaskNotificationOverrideResponse> UpdateTaskOverrideAsync(
        Guid userId,
        Guid taskId,
        UpdateTaskNotificationOverrideRequest request,
        CancellationToken cancellationToken)
    {
        await GetTaskForUserAsync(userId, taskId, requireOwner: true, cancellationToken);
        var notificationOverride = await dbContext.TaskNotificationOverrides
            .FirstOrDefaultAsync(candidate => candidate.TaskId == taskId, cancellationToken);
        var now = DateTime.UtcNow;

        if (notificationOverride is null)
        {
            notificationOverride = new TaskNotificationOverride
            {
                TaskId = taskId,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.TaskNotificationOverrides.Add(notificationOverride);
        }

        ApplyOverride(notificationOverride, request, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(taskId, notificationOverride);
    }

    public async Task DeleteTaskOverrideAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        await GetTaskForUserAsync(userId, taskId, requireOwner: true, cancellationToken);
        var notificationOverride = await dbContext.TaskNotificationOverrides
            .FirstOrDefaultAsync(candidate => candidate.TaskId == taskId, cancellationToken);
        if (notificationOverride is null)
        {
            return;
        }

        dbContext.TaskNotificationOverrides.Remove(notificationOverride);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserNotificationPreference> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userExists = await dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
        if (!userExists)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        var preference = await dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (preference is not null)
        {
            return preference;
        }

        var now = DateTime.UtcNow;
        preference = new UserNotificationPreference
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.UserNotificationPreferences.Add(preference);
        return preference;
    }

    private async Task<DoItTask> GetTaskForUserAsync(
        Guid userId,
        Guid taskId,
        bool requireOwner,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .AsNoTracking()
            .Include(candidate => candidate.Assignments)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);

        if (task is null || !CanViewTask(task, userId) || requireOwner && task.CreatedByUserId != userId)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        return task;
    }

    private static bool CanViewTask(DoItTask task, Guid userId)
    {
        if (task.CreatedByUserId == userId)
        {
            return true;
        }

        if (task.Scope != TaskScope.House)
        {
            return false;
        }

        return task.AssignmentMode == AssignmentMode.Anyone
            || task.Assignments.Any(assignment => assignment.UserId == userId);
    }

    private static void ApplyOverride(TaskNotificationOverride notificationOverride, UpdateTaskNotificationOverrideRequest request, DateTime now)
    {
        notificationOverride.AvailableFromEnabled = request.AvailableFromEnabled;
        notificationOverride.RecommendedEnabled = request.RecommendedEnabled;
        notificationOverride.BeforeAvailableUntilEnabled = request.BeforeAvailableUntilEnabled;
        notificationOverride.TaskExpiredEnabled = request.TaskExpiredEnabled;
        notificationOverride.TaskCompletedEnabled = request.TaskCompletedEnabled;
        notificationOverride.BeforeAvailableUntilMinutes = request.BeforeAvailableUntilMinutes is null
            ? null
            : NormalizeOffset(request.BeforeAvailableUntilMinutes.Value);
        notificationOverride.UpdatedAt = now;
    }

    private static int NormalizeOffset(int minutes)
    {
        if (minutes is < TaskNotificationService.MinimumEndOffsetMinutes or > TaskNotificationService.MaximumEndOffsetMinutes)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "invalid_notification_offset",
                $"The notification offset must be between {TaskNotificationService.MinimumEndOffsetMinutes} and {TaskNotificationService.MaximumEndOffsetMinutes} minutes.");
        }

        return minutes;
    }

    private static NotificationPreferenceResponse ToResponse(UserNotificationPreference preference) => new(
        preference.AvailableFromEnabled,
        preference.RecommendedEnabled,
        preference.BeforeAvailableUntilEnabled,
        preference.TaskExpiredEnabled,
        preference.TaskCompletedEnabled,
        preference.BeforeAvailableUntilMinutes);

    private static TaskNotificationOverrideResponse ToResponse(Guid taskId, TaskNotificationOverride? notificationOverride) => new(
        taskId,
        notificationOverride?.AvailableFromEnabled,
        notificationOverride?.RecommendedEnabled,
        notificationOverride?.BeforeAvailableUntilEnabled,
        notificationOverride?.TaskExpiredEnabled,
        notificationOverride?.TaskCompletedEnabled,
        notificationOverride?.BeforeAvailableUntilMinutes);
}
