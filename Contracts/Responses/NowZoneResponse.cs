namespace DoIt.Api.Contracts.Responses;

public sealed record NowZoneResponse(
    Guid? ZoneId,
    string ZoneName,
    NowProgressResponse Progress,
    IReadOnlyList<NowTaskResponse> Overdue,
    IReadOnlyList<NowTaskResponse> Available,
    IReadOnlyList<NowTaskResponse> Unavailable,
    IReadOnlyList<NowTaskResponse> Completed);
