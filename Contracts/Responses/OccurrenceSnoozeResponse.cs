namespace DoIt.Api.Contracts.Responses;

public sealed record OccurrenceSnoozeResponse(
    Guid OccurrenceId,
    Guid TaskId,
    DateOnly Date,
    DateTime SnoozedUntilUtc);
