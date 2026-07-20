namespace DoIt.Api.Contracts.Responses;

public sealed record NowProgressResponse(
    int Total,
    int Done,
    int Missed,
    int NotApplicable,
    int Pending);
