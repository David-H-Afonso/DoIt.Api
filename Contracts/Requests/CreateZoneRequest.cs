namespace DoIt.Api.Contracts.Requests;

public sealed record CreateZoneRequest(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    int? SortOrder);
