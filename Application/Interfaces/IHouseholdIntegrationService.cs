using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface IHouseholdIntegrationService
{
    Task<NowResponse> GetSummaryAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);
    Task<NowResponse> GetNowAsync(Guid userId, DateOnly? date, CancellationToken cancellationToken);
    Task<HouseholdOccurrenceActionResponse> CompleteOccurrenceAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<HouseholdOccurrenceActionResponse> UndoOccurrenceAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> CompleteTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> UndoTaskAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task<TaskResponse> CreateTaskAsync(Guid userId, HouseholdCreateTaskRequest request, CancellationToken cancellationToken);
}
