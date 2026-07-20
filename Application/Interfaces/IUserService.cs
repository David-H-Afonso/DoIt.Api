using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid userId, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken);
}
