using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(User user, DateTime expiresAt);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
