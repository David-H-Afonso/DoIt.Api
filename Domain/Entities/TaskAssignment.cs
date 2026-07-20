using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Domain.Entities;

public sealed class TaskAssignment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public TaskAssignmentRole Role { get; set; } = TaskAssignmentRole.Primary;
    public DateTime CreatedAt { get; set; }

    public DoItTask? Task { get; set; }
    public User? User { get; set; }
}
