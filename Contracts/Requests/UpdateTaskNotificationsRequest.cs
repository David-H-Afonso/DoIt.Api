namespace DoIt.Api.Contracts.Requests;

public sealed record UpdateTaskNotificationsRequest(IReadOnlyList<TaskNotificationUpdateRequest> Tasks);

public sealed record TaskNotificationUpdateRequest(
    Guid TaskId,
    UpdateTaskNotificationOverrideRequest Settings);
