using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<NotificationPreferenceResponse> UpdateAsync(Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken);
    Task<TaskNotificationOverrideResponse> GetTaskOverrideAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
    Task<TaskNotificationOverrideResponse> UpdateTaskOverrideAsync(Guid userId, Guid taskId, UpdateTaskNotificationOverrideRequest request, CancellationToken cancellationToken);
    Task DeleteTaskOverrideAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
