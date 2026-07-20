using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IReviewService
{
    Task<ReviewResponse> GetReviewAsync(Guid userId, DateOnly date, CancellationToken cancellationToken);
}
