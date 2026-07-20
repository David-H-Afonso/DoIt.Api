using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateUserRequest(
    [Required] [MinLength(1)] string DisplayName,
    string? Locale,
    string? Role,
    bool IsActive);
