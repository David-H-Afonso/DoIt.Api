using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<TaskResponse> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken);
    Task<TaskResponse> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task RestoreAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
