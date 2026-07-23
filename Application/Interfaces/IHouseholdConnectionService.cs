using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IHouseholdConnectionService
{
    Task<HouseholdAuthorizeResponse> AuthorizeAsync(Guid userId, HouseholdAuthorizeRequest request, CancellationToken cancellationToken);
    Task<HouseholdTokenResponse> ExchangeAsync(HouseholdTokenRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(HouseholdRevokeRequest request, CancellationToken cancellationToken);
    Task<HouseholdMeResponse> GetMeAsync(Guid connectionId, CancellationToken cancellationToken);
}
