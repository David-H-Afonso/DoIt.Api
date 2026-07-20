namespace DoIt.Api.Domain.Entities;

public sealed class XpEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid TaskId { get; set; }
    public Guid CompletionId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string Importance { get; set; } = string.Empty;
    public int FormulaVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime? RevertedAt { get; set; }

    public User? User { get; set; }
    public TaskOccurrence? Occurrence { get; set; }
    public DoItTask? Task { get; set; }
    public TaskCompletion? Completion { get; set; }
}
