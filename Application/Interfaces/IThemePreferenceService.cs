using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IThemePreferenceService
{
    Task<ThemePreferenceResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<ThemePreferenceResponse> UpdateAsync(Guid userId, UpdateThemePreferenceRequest request, CancellationToken cancellationToken);
}
