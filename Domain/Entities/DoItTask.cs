using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class DoItTask
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ZoneId { get; set; }
    public TaskScope Scope { get; set; } = TaskScope.Personal;
    public TaskType TaskType { get; set; } = TaskType.OneTime;
    public TaskImportance Importance { get; set; } = TaskImportance.Normal;
    public TaskComplexity Complexity { get; set; } = TaskComplexity.Easy;
    public TaskObligation Obligation { get; set; } = TaskObligation.Required;
    public AssignmentMode AssignmentMode { get; set; } = AssignmentMode.SingleUser;
    public bool IsArchived { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public Zone? Zone { get; set; }
    public TaskSchedule? Schedule { get; set; }
    public ICollection<TaskOccurrence> Occurrences { get; set; } = new List<TaskOccurrence>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
}
