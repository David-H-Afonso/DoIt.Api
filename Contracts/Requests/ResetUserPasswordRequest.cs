using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record ResetUserPasswordRequest(
    [Required] [MinLength(8)] string Password);
