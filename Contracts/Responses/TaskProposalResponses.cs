using DoIt.Api.Contracts.Requests;

namespace DoIt.Api.Contracts.Responses;

public sealed record TaskProposalResponse(
    Guid Id,
    Guid ProposerUserId,
    Guid TargetUserId,
    string Status,
    CreateTaskRequest Draft,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    Guid? ResultingTaskId);
