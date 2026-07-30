using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Interfaces;

public interface ITaskNotificationService
{
    Task EnsureScheduledDeliveriesAsync(DateTime now, CancellationToken cancellationToken);
    Task QueueTaskCompletedAsync(TaskOccurrence occurrence, TaskCompletion completion, CancellationToken cancellationToken);
    Task<string?> BuildPayloadIfEligibleAsync(NotificationDelivery delivery, DateTime now, CancellationToken cancellationToken);
}
