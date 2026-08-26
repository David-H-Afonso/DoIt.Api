using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class TaskProposal
{
    public Guid Id { get; set; }
    public Guid ProposerUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public TaskProposalStatus Status { get; set; } = TaskProposalStatus.Pending;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ZoneId { get; set; }
    public string? Scope { get; set; }
    public string? TaskType { get; set; }
    public string? Importance { get; set; }
    public string? Complexity { get; set; }
    public string? Obligation { get; set; }
    public string? AssignmentMode { get; set; }
    public string? AssigneeIdsJson { get; set; }
    public string? ScheduleJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResultingTaskId { get; set; }

    public User? ProposerUser { get; set; }
    public User? TargetUser { get; set; }
}
