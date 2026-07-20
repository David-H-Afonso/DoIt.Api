namespace DoIt.Api.Contracts.Responses;

public sealed record AuthResponse(
    UserResponse User,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
