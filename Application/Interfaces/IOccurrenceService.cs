using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Interfaces;

public interface IOccurrenceService
{
    Task<TaskOccurrence> GetOrCreateAsync(DoItTask task, DateOnly date, DateTime now, CancellationToken cancellationToken);
}
