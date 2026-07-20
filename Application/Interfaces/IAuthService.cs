using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
