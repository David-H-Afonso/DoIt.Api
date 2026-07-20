namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateZoneRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    int SortOrder);
