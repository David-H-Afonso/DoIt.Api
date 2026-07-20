namespace DoIt.Api.Contracts.Responses;

public sealed record NowResponse(
    DateOnly Date,
    string Scope,
    NowProgressResponse Progress,
    IReadOnlyList<NowZoneResponse> Zones);
