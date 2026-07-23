using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class HouseholdIntegrationService(
    DoItDbContext dbContext,
    INowService nowService,
    ITaskService taskService,
    ITaskActionService taskActionService,
    IOccurrenceService occurrenceService) : IHouseholdIntegrationService
{
    public async Task<NowResponse> GetSummaryAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken)
    {
        await EnsureIntegrationUserAsync(userId, cancellationToken);
        var response = await nowService.GetNowAsync(userId, date, "house-assigned", cancellationToken);
        return response with { Scope = "house" };
    }

    public async Task<OccurrenceActionResponse> CompleteTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var occurrence = await GetCurrentHouseholdOccurrenceAsync(userId, taskId, allowArchived: true, cancellationToken);
        if (occurrence.Task!.IsArchived && occurrence.Status != OccurrenceStatus.Done)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        return await taskActionService.CompleteAsync(userId, occurrence.Id, cancellationToken);
    }

    public async Task<OccurrenceActionResponse> UndoTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var occurrence = await GetCurrentHouseholdOccurrenceAsync(userId, taskId, allowArchived: true, cancellationToken);
        return await taskActionService.UndoAsync(userId, occurrence.Id, cancellationToken);
    }

    public async Task<TaskResponse> CreateTaskAsync(Guid userId, HouseholdCreateTaskRequest request, CancellationToken cancellationToken)
    {
        await EnsureIntegrationUserAsync(userId, cancellationToken);
        var createRequest = new CreateTaskRequest(
            request.Title,
            request.Description,
            request.ZoneId,
            "House",
            request.TaskType,
            request.Importance,
            request.Complexity,
            request.Obligation,
            request.Schedule,
            AssignmentMode.Anyone.ToString());

        return await taskService.CreateAsync(userId, createRequest, cancellationToken);
    }

    private async Task<TaskOccurrence> GetCurrentHouseholdOccurrenceAsync(Guid userId, Guid taskId, bool allowArchived, CancellationToken cancellationToken)
    {
        await EnsureIntegrationUserAsync(userId, cancellationToken);
        var task = await dbContext.Tasks
            .Include(candidate => candidate.Schedule)
            .Include(candidate => candidate.Assignments)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId && candidate.Scope == TaskScope.House, cancellationToken);

        if (task is null ||
            task.Schedule is null ||
            !allowArchived && task.IsArchived ||
            task.AssignmentMode != AssignmentMode.Anyone && !task.Assignments.Any(assignment => assignment.UserId == userId))
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_found", "Task not found.");
        }

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneHelper.Find(task.Schedule.TimeZoneId)).Date);
        var occurrenceDate = task.Schedule.RecurrenceType == RecurrenceType.Manual
            ? task.Schedule.StartDate
            : localToday;
        if (!RecurrenceRules.AppliesOnDate(task.Schedule, occurrenceDate))
        {
            throw new ApiException(StatusCodes.Status404NotFound, "task_not_available", "Task is not available for the current date.");
        }

        return await occurrenceService.GetOrCreateAsync(task, occurrenceDate, DateTime.UtcNow, cancellationToken);
    }

    private async Task EnsureIntegrationUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || !await dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "integration_user_unavailable", "Household integration user is unavailable.");
        }
    }
}
