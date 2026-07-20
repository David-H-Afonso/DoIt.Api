namespace DoIt.Api.Contracts.Responses;

public sealed record ReviewResponse(
    DateOnly Date,
    int XpEarned,
    IReadOnlyList<ReviewTaskResponse> Done,
    IReadOnlyList<ReviewTaskResponse> Missed,
    IReadOnlyList<ReviewTaskResponse> NotApplicable,
    IReadOnlyList<ReviewTaskResponse> Pending,
    IReadOnlyList<ReviewZoneResponse> ByZone,
    IReadOnlyList<ReviewTaskResponse> Created);
