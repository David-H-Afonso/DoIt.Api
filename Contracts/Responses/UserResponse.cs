namespace DoIt.Api.Contracts.Responses;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    string PreferredLocale,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);
