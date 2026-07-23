namespace DoIt.Api.Contracts.Responses;

public sealed record TaskOccurrenceSummaryResponse(
    Guid Id,
    DateOnly Date,
    string Status,
    DateTime? CompletedAt,
    Guid? CompletedByUserId);
