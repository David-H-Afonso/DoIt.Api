using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface ITaskActionService
{
    Task<OccurrenceActionResponse> CompleteAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> CompleteEarlyAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> MissAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> NotApplicableAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
    Task<OccurrenceActionResponse> UndoAsync(Guid userId, Guid occurrenceId, CancellationToken cancellationToken);
}
