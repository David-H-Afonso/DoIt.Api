namespace DoIt.Api.Contracts.Responses;

public sealed record ReviewTaskResponse(
    Guid OccurrenceId,
    Guid TaskId,
    string Title,
    string? ZoneName,
    string Status,
    string? CompletedBy,
    int XpEarned,
    DateTime TaskCreatedAt,
    DateTime? CompletedAt);
