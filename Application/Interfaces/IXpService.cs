using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Interfaces;

public interface IXpService
{
    Task<(int Amount, UserXpResponse UserXp)> AwardCompletionAsync(TaskOccurrence occurrence, TaskCompletion completion, CancellationToken cancellationToken);
    Task<UserXpResponse?> RevertCompletionAsync(TaskCompletion completion, CancellationToken cancellationToken);
    Task<UserXpResponse> GetUserXpAsync(Guid userId, CancellationToken cancellationToken);
}
