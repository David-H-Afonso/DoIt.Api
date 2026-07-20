using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);
