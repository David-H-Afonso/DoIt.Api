namespace DoIt.Api.Contracts.Responses;

public sealed record ZoneResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    int SortOrder,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt);
