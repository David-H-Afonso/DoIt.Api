namespace DoIt.Api.Contracts.Requests;

public sealed record CreateTaskProposalRequest(
    Guid TargetUserId,
    string Title,
    string? Description,
    Guid? ZoneId,
    string? Scope,
    string? TaskType,
    string? Importance,
    string? Complexity,
    string? Obligation,
    TaskScheduleRequest? Schedule,
    string? AssignmentMode = null,
    IReadOnlyList<Guid>? AssigneeIds = null);
