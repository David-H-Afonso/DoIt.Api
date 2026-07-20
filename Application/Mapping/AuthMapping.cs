using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Mapping;

public static class AuthMapping
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role.ToString(),
            user.PreferredLocale,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt);
    }
}
