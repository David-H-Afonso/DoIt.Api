namespace DoIt.Api.Domain.Entities;

public sealed class TaskOccurrenceSnooze
{
    public Guid Id { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid UserId { get; set; }
    public DateTime UntilAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TaskOccurrence? Occurrence { get; set; }
    public User? User { get; set; }
}
