using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class TaskOccurrence
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public DateOnly Date { get; set; }
    public OccurrenceStatus Status { get; set; } = OccurrenceStatus.Pending;
    public DateTime? AvailableFromAt { get; set; }
    public DateTime? AvailableUntilAt { get; set; }
    public DateTime? RecommendedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DoItTask? Task { get; set; }
    public ICollection<TaskCompletion> Completions { get; set; } = new List<TaskCompletion>();
}
