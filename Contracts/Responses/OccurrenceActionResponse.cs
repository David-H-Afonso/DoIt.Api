namespace DoIt.Api.Contracts.Responses;

public sealed record OccurrenceActionResponse(
    Guid OccurrenceId,
    Guid TaskId,
    DateOnly Date,
    string Status,
    int XpEarned,
    UserXpResponse? UserXp);
