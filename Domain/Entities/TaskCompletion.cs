using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class TaskCompletion
{
    public Guid Id { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid UserId { get; set; }
    public TaskCompletionAction Action { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevertedAt { get; set; }

    public TaskOccurrence? Occurrence { get; set; }
    public User? User { get; set; }
}
