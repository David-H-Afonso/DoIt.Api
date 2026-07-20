namespace DoIt.Api.Contracts.Responses;

public sealed record ReviewZoneResponse(
    Guid? ZoneId,
    string ZoneName,
    int Total,
    int Done,
    int Missed,
    int NotApplicable,
    int Pending);
