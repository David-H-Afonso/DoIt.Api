using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface ITaskProposalService
{
    Task<TaskProposalResponse> CreateAsync(Guid proposerUserId, CreateTaskProposalRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskProposalResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<TaskProposalResponse> GetAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken);
    Task<TaskProposalResponse> AcceptAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken);
    Task<TaskProposalResponse> RejectAsync(Guid userId, Guid proposalId, CancellationToken cancellationToken);
}
