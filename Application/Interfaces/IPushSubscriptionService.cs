using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IPushSubscriptionService
{
    WebPushConfigResponse GetPublicConfiguration();
    Task<PushSubscriptionStatusResponse> UpsertAsync(Guid userId, PushSubscriptionRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid userId, DeletePushSubscriptionRequest request, CancellationToken cancellationToken);
}
