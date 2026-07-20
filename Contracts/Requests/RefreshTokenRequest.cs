using System.ComponentModel.DataAnnotations;

namespace DoIt.Api.Contracts.Requests;

public sealed record RefreshTokenRequest([Required] string RefreshToken);
