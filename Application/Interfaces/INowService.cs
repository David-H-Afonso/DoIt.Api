using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface INowService
{
    Task<NowResponse> GetNowAsync(Guid userId, DateOnly? date, string? scope, CancellationToken cancellationToken);
}
