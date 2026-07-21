using DoIt.Api.Application.Interfaces;
using DoIt.Api.Application.Mapping;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class TaskService(DoItDbContext dbContext, IOccurrenceService occurrenceService) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tasks = await QueryUserTasks(userId)
            .OrderBy(task => task.IsArchived)
            .ThenBy(task => task.Zone == null ? int.MaxValue : task.Zone.SortOrder)
            .ThenBy(task => task.Title)
            .ToListAsync(cancellationToken);

        var responses = new List<TaskResponse>(tasks.Count);
        foreach (var task in tasks)
        {
            var response = task.ToResponse();
            if (task.Schedule?.RecurrenceType == RecurrenceType.Manual)
            {
                var date = task.Schedule.StartDate;
                var occurrence = await dbContext.TaskOccurrences
                    .Include(candidate => candidate.Completions)
                    .FirstOrDefaultAsync(candidate => candidate.TaskId == task.Id && candidate.Date == date, cancellationToken);
                occurrence ??= await occurrenceService.GetOrCreateAsync(task, date, DateTime.UtcNow, cancellationToken);
                var completion = occurrence?.Completions
                    .Where(candidate => candidate.RevertedAt == null && candidate.Action == TaskCompletionAction.Done)
                    .OrderByDescending(candidate => candidate.CreatedAt)
                    .FirstOrDefault();
                responses.Add(response with
                {
                    OccurrenceDate = date,
                    OccurrenceStatus = occurrence?.Status.ToString() ?? OccurrenceStatus.Pending.ToString(),
                    OccurrenceCompletedAt = completion?.CreatedAt,
                    OccurrenceId = occurrence?.Id
                });
                continue;
            }

            responses.Add(response);
        }

        return responses;
    }

    public async Task<TaskResponse> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await QueryUserTasks(userId).FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
        if (task is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        var response = task.ToResponse();
        if (task.Schedule?.RecurrenceType != RecurrenceType.Manual)
        {
            return response;
        }

        var date = task.Schedule.StartDate;
        var occurrence = await dbContext.TaskOccurrences
            .Include(candidate => candidate.Completions)
            .FirstOrDefaultAsync(candidate => candidate.TaskId == task.Id && candidate.Date == date, cancellationToken);
        occurrence ??= await occurrenceService.GetOrCreateAsync(task, date, DateTime.UtcNow, cancellationToken);
        var completion = occurrence.Completions
            .Where(candidate => candidate.RevertedAt is null && candidate.Action == TaskCompletionAction.Done)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefault();
        return response with
        {
            OccurrenceDate = date,
            OccurrenceStatus = occurrence.Status.ToString(),
            OccurrenceCompletedAt = completion?.CreatedAt,
            OccurrenceId = occurrence.Id
        };
    }

    public async Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        ValidateTitle(request.Title);
        var title = request.Title.Trim();
        var normalizedTitle = title.ToLowerInvariant();
        if (await QueryUserTasks(userId).AnyAsync(task => task.Title.ToLower() == normalizedTitle, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "task_exists", "A task with this title already exists.");
        }

        await ValidateZoneAsync(userId, request.ZoneId, cancellationToken);

        var now = DateTime.UtcNow;
        var scope = ParseEnum<TaskScope>(request.Scope, TaskScope.Personal);
        var task = new DoItTask
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = NormalizeOptional(request.Description),
            ZoneId = request.ZoneId,
            Scope = scope,
            TaskType = ParseEnum<TaskType>(request.TaskType, TaskType.OneTime),
            Importance = ParseEnum<TaskImportance>(request.Importance, TaskImportance.Normal),
            Complexity = ParseEnum<TaskComplexity>(request.Complexity, TaskComplexity.Easy),
            Obligation = ParseEnum<TaskObligation>(request.Obligation, TaskObligation.Required),
            AssignmentMode = ResolveAssignmentMode(ParseEnum<AssignmentMode>(request.AssignmentMode, AssignmentMode.SingleUser), scope, request.AssigneeIds),
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            Schedule = CreateSchedule(request.Schedule, now)
        };

        await ApplyAssignmentsAsync(task, userId, request.AssigneeIds, now, cancellationToken);

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, task.Id, cancellationToken);
    }

    public async Task<TaskResponse> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        ValidateTitle(request.Title);
        await ValidateZoneAsync(userId, request.ZoneId, cancellationToken);

        var task = await QueryUserTasks(userId).FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
        if (task is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        var now = DateTime.UtcNow;
        task.Title = request.Title.Trim();
        task.Description = NormalizeOptional(request.Description);
        task.ZoneId = request.ZoneId;
        task.Scope = ParseEnum<TaskScope>(request.Scope, task.Scope);
        task.TaskType = ParseEnum<TaskType>(request.TaskType, task.TaskType);
        task.Importance = ParseEnum<TaskImportance>(request.Importance, task.Importance);
        task.Complexity = ParseEnum<TaskComplexity>(request.Complexity, task.Complexity);
        task.Obligation = ParseEnum<TaskObligation>(request.Obligation, task.Obligation);
        task.AssignmentMode = ResolveAssignmentMode(ParseEnum<AssignmentMode>(request.AssignmentMode, task.AssignmentMode), task.Scope, request.AssigneeIds);
        task.UpdatedAt = now;

        if (task.Schedule is null)
        {
            task.Schedule = CreateSchedule(request.Schedule, now);
        }
        else
        {
            ApplySchedule(task.Schedule, request.Schedule, now);
        }

        await ApplyAssignmentsAsync(task, userId, request.AssigneeIds, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, task.Id, cancellationToken);
    }

    public async Task ArchiveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await QueryUserTasks(userId).FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        task.IsArchived = true;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await QueryUserTasks(userId).FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await QueryUserTasks(userId).FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        task.IsArchived = false;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<DoItTask> QueryUserTasks(Guid userId)
    {
        return dbContext.Tasks
            .Include(task => task.Zone)
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .Where(task => task.CreatedByUserId == userId);
    }

    private async Task ValidateZoneAsync(Guid userId, Guid? zoneId, CancellationToken cancellationToken)
    {
        if (zoneId is null)
        {
            return;
        }

        var exists = await dbContext.Zones.AnyAsync(zone => zone.Id == zoneId && zone.CreatedByUserId == userId && !zone.IsArchived, cancellationToken);
        if (!exists)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_zone", "Zone does not exist.");
        }
    }


    private static TaskSchedule CreateSchedule(TaskScheduleRequest? request, DateTime now)
    {
        var schedule = new TaskSchedule { Id = Guid.NewGuid(), CreatedAt = now };
        ApplySchedule(schedule, request, now);
        return schedule;
    }

    private static void ApplySchedule(TaskSchedule schedule, TaskScheduleRequest? request, DateTime now)
    {
        var recurrenceType = ParseEnum(request?.RecurrenceType, RecurrenceType.Manual);
        var startDate = request?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = request?.EndDate;
        var weekday = request?.Weekday;
        var weekOfMonth = request?.WeekOfMonth;
        var timesPerWeek = request?.TimesPerWeek;
        var everyNDays = request?.EveryNDays;
        var availableFrom = request?.AvailableFromTime;
        var availableUntil = request?.AvailableUntilTime;
        var recommended = request?.RecommendedTime;
        var timeZoneId = TimeZoneHelper.Normalize(request?.TimeZoneId);
        var unavailableMode = ParseEnum(request?.UnavailableVisibilityMode, UnavailableVisibilityMode.Dimmed);

        ValidateSchedule(recurrenceType, availableFrom, availableUntil, weekday, timesPerWeek, everyNDays, weekOfMonth);

        schedule.RecurrenceType = recurrenceType;
        schedule.StartDate = startDate;
        schedule.EndDate = endDate;
        schedule.Weekday = weekday;
        schedule.WeekOfMonth = weekOfMonth;
        schedule.TimesPerWeek = timesPerWeek;
        schedule.EveryNDays = everyNDays;
        schedule.AvailableFromTime = availableFrom;
        schedule.AvailableUntilTime = availableUntil;
        schedule.RecommendedTime = recommended;
        schedule.TimeZoneId = timeZoneId;
        schedule.UnavailableVisibilityMode = unavailableMode;
        schedule.UpdatedAt = now;
    }

    private async Task ApplyAssignmentsAsync(DoItTask task, Guid creatorUserId, IReadOnlyList<Guid>? assigneeIds, DateTime now, CancellationToken cancellationToken)
    {
        var ids = ResolveAssigneeIds(task.Scope, task.AssignmentMode, creatorUserId, assigneeIds);
        var activeUsers = await dbContext.Users
            .Where(user => ids.Contains(user.Id) && user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (activeUsers.Count != ids.Count)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_assignee", "One or more assignees do not exist.");
        }

        var desiredIds = ids.ToHashSet();
        foreach (var assignment in task.Assignments.Where(assignment => !desiredIds.Contains(assignment.UserId)).ToList())
        {
            dbContext.TaskAssignments.Remove(assignment);
        }

        var existingByUserId = task.Assignments.ToDictionary(assignment => assignment.UserId);
        for (var index = 0; index < ids.Count; index++)
        {
            var assignedUserId = ids[index];
            if (existingByUserId.TryGetValue(assignedUserId, out var existing))
            {
                existing.Role = index == 0 ? TaskAssignmentRole.Primary : TaskAssignmentRole.Participant;
                continue;
            }

            task.Assignments.Add(new TaskAssignment
            {
                Id = Guid.NewGuid(),
                UserId = assignedUserId,
                Role = index == 0 ? TaskAssignmentRole.Primary : TaskAssignmentRole.Participant,
                CreatedAt = now
            });
        }
    }

    private static AssignmentMode ResolveAssignmentMode(AssignmentMode requested, TaskScope scope, IReadOnlyList<Guid>? assigneeIds)
    {
        if (scope == TaskScope.House && (assigneeIds is null || assigneeIds.Count == 0) && requested == AssignmentMode.SingleUser)
        {
            return AssignmentMode.Anyone;
        }

        return requested;
    }

    private static IReadOnlyList<Guid> ResolveAssigneeIds(TaskScope scope, AssignmentMode mode, Guid creatorUserId, IReadOnlyList<Guid>? assigneeIds)
    {
        if (scope == TaskScope.Personal)
        {
            return [creatorUserId];
        }

        if (mode == AssignmentMode.Anyone)
        {
            return [];
        }

        var ids = (assigneeIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "assignee_required", "Assignment mode requires at least one assignee.");
        }

        return ids;
    }

    private static void ValidateSchedule(RecurrenceType recurrenceType, TimeOnly? availableFrom, TimeOnly? availableUntil, DayOfWeek? weekday, int? timesPerWeek, int? everyNDays, int? weekOfMonth)
    {
        if ((recurrenceType == RecurrenceType.Weekday || recurrenceType == RecurrenceType.MonthlyOrdinalWeekday) && weekday is null)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "weekday_required", "Weekday recurrence requires a weekday.");
        }

        if (recurrenceType == RecurrenceType.MonthlyOrdinalWeekday && weekOfMonth is < 1 or > 4)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "week_of_month_required", "Monthly weekday recurrence requires an ordinal from one to four.");
        }

        if (recurrenceType == RecurrenceType.TimesPerWeek && (timesPerWeek is null or <= 0))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "times_per_week_required", "Times per week recurrence requires a positive value.");
        }

        if (recurrenceType == RecurrenceType.EveryNDays && (everyNDays is null or <= 0))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "every_n_days_required", "Every N days recurrence requires a positive value.");
        }

        if (availableFrom is not null && availableUntil is not null && availableUntil <= availableFrom)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_time_window", "Available until must be after available from.");
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "title_required", "Task title is required.");
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<TEnum>(normalized, true, out var parsed) ? parsed : fallback;
    }
}
