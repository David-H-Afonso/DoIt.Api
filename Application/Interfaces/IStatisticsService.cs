using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IStatisticsService
{
    Task<StatisticsResponse> GetAsync(Guid userId, DateOnly from, DateOnly to, string? groupBy, CancellationToken cancellationToken);
}
