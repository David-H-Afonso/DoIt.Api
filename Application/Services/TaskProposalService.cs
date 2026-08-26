using System.Text.Json;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class TaskProposalService(
    DoItDbContext dbContext,
    ITaskService taskService,
    INotificationInboxService inboxService,
    TimeProvider timeProvider) : ITaskProposalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TaskProposalResponse> CreateAsync(Guid proposerUserId, CreateTaskProposalRequest request, CancellationToken cancellationToken)
    {
        var target = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == request.TargetUserId && user.IsActive, cancellationToken);
        if (target is null || target.Id == proposerUserId)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_proposal_target", "Proposal target must be another active user.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 220)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_proposal_title", "Proposal title is required and must be 220 characters or fewer.");
        }

        if (request.Description?.Length > 2000)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_proposal_description", "Proposal description must be 2000 characters or fewer.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var proposal = new TaskProposal
        {
            Id = Guid.NewGuid(), ProposerUserId = proposerUserId, TargetUserId = target.Id,
            Title = request.Title.Trim(), Description = request.Description?.Trim(), ZoneId = request.ZoneId,
            Scope = request.Scope, TaskType = request.TaskType, Importance = request.Importance, Complexity = request.Complexity,
            Obligation = request.Obligation, AssignmentMode = request.AssignmentMode,
            AssigneeIdsJson = JsonSerializer.Serialize(request.AssigneeIds, JsonOptions),
            ScheduleJson = JsonSerializer.Serialize(request.Schedule, JsonOptions), CreatedAt = now, UpdatedAt = now
        };
        dbContext.TaskProposals.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyAsync(proposal, "Nueva propuesta de tarea", $"{request.Title.Trim()} te ha sido propuesta.", cancellationToken);
        return ToResponse(proposal);
    }

    public async Task<IReadOnlyList<TaskProposalResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var proposals = await dbContext.TaskProposals.AsNoTracking()
            .Where(proposal => proposal.TargetUserId == userId || proposal.ProposerUserId == userId)
            .OrderByDescending(proposal => proposal.CreatedAt).ToListAsync(cancellationToken);
        return proposals.Select(ToResponse).ToList();
    }

    public async Task<TaskProposalResponse> GetAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken)
    {
        var proposal = await GetVisibleAsync(userId, proposalId, cancellationToken);
        return ToResponse(proposal);
    }

    public async Task<TaskProposalResponse> AcceptAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var proposal = await GetVisibleAsync(userId, proposalId, cancellationToken);
        if (proposal.TargetUserId != userId)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "proposal_not_found", "Task proposal not found.");
        }
        if (proposal.Status != TaskProposalStatus.Pending)
        {
            return ToResponse(proposal);
        }

        var claimed = await dbContext.TaskProposals
            .Where(candidate => candidate.Id == proposalId && candidate.TargetUserId == userId && candidate.Status == TaskProposalStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, TaskProposalStatus.Accepted)
                .SetProperty(candidate => candidate.ResolvedAt, timeProvider.GetUtcNow().UtcDateTime)
                .SetProperty(candidate => candidate.UpdatedAt, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        if (claimed != 1)
        {
            dbContext.ChangeTracker.Clear();
            return ToResponse(await GetVisibleAsync(userId, proposalId, cancellationToken));
        }

        var task = await taskService.CreateAsync(userId, ToDraft(proposal, proposal.ProposerUserId), cancellationToken);
        proposal.Status = TaskProposalStatus.Accepted;
        proposal.ResolvedAt ??= timeProvider.GetUtcNow().UtcDateTime;
        proposal.UpdatedAt = proposal.ResolvedAt.Value;
        proposal.ResultingTaskId = task.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyAsync(proposal, "Propuesta aceptada", $"{await GetDisplayNameAsync(userId, cancellationToken)} ha aceptado tu propuesta: {proposal.Title}.", cancellationToken, task.Id);
        return ToResponse(proposal);
    }

    public async Task<TaskProposalResponse> RejectAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var proposal = await GetVisibleAsync(userId, proposalId, cancellationToken);
        if (proposal.TargetUserId != userId)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "proposal_not_found", "Task proposal not found.");
        }
        if (proposal.Status != TaskProposalStatus.Pending)
        {
            return ToResponse(proposal);
        }

        var resolvedAt = timeProvider.GetUtcNow().UtcDateTime;
        var claimed = await dbContext.TaskProposals
            .Where(candidate => candidate.Id == proposalId && candidate.TargetUserId == userId && candidate.Status == TaskProposalStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.Status, TaskProposalStatus.Rejected)
                .SetProperty(candidate => candidate.ResolvedAt, resolvedAt)
                .SetProperty(candidate => candidate.UpdatedAt, resolvedAt), cancellationToken);
        if (claimed != 1)
        {
            dbContext.ChangeTracker.Clear();
            return ToResponse(await GetVisibleAsync(userId, proposalId, cancellationToken));
        }

        proposal.Status = TaskProposalStatus.Rejected;
        proposal.ResolvedAt = resolvedAt;
        proposal.UpdatedAt = resolvedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyAsync(proposal, "Propuesta rechazada", $"{await GetDisplayNameAsync(userId, cancellationToken)} ha rechazado tu propuesta: {proposal.Title}.", cancellationToken);
        return ToResponse(proposal);
    }

    private async Task<TaskProposal> GetVisibleAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken)
        => await dbContext.TaskProposals.FirstOrDefaultAsync(proposal => proposal.Id == proposalId && (proposal.TargetUserId == userId || proposal.ProposerUserId == userId), cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "proposal_not_found", "Task proposal not found.");

    private async Task NotifyAsync(TaskProposal proposal, string title, string body, CancellationToken cancellationToken, Guid? taskId = null)
    {
        var sourceType = proposal.Status == TaskProposalStatus.Pending ? "TaskProposalCreated" : proposal.Status == TaskProposalStatus.Accepted ? "TaskProposalAccepted" : "TaskProposalRejected";
        await inboxService.CreateAsync(
            proposal.Status == TaskProposalStatus.Pending ? proposal.TargetUserId : proposal.ProposerUserId,
            sourceType, proposal.Id, $"task-proposal:{proposal.Id:N}:{sourceType}", title, body,
            "/tasks/proposals", new { sourceType, proposalId = proposal.Id, taskId },
            timeProvider.GetUtcNow().UtcDateTime, true, cancellationToken);
    }

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
        => await dbContext.Users.Where(user => user.Id == userId).Select(user => user.DisplayName).FirstAsync(cancellationToken);

    private static CreateTaskRequest ToDraft(TaskProposal proposal, Guid? excludedUserId = null)
        => new(proposal.Title, proposal.Description, proposal.ZoneId, proposal.Scope, proposal.TaskType, proposal.Importance, proposal.Complexity, proposal.Obligation,
            proposal.ScheduleJson is null ? null : JsonSerializer.Deserialize<TaskScheduleRequest>(proposal.ScheduleJson, JsonOptions), proposal.AssignmentMode,
            proposal.AssigneeIdsJson is null ? null : JsonSerializer.Deserialize<IReadOnlyList<Guid>>(proposal.AssigneeIdsJson, JsonOptions)?.Where(id => id != excludedUserId).ToList());

    private static TaskProposalResponse ToResponse(TaskProposal proposal) => new(proposal.Id, proposal.ProposerUserId, proposal.TargetUserId, proposal.Status.ToString(), ToDraft(proposal), proposal.CreatedAt, proposal.UpdatedAt, proposal.ResolvedAt, proposal.ResultingTaskId);
}
