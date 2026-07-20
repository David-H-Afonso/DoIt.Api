using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record RegisterRequest(
    [Required] [MinLength(3)] string Username,
    [Required] [MinLength(1)] string DisplayName,
    [Required] [MinLength(8)] string Password,
    string? Locale);
