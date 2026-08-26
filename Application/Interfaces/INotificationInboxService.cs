using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface INotificationInboxService
{
    Task<IReadOnlyList<NotificationInboxItemResponse>> ListAsync(Guid userId, bool unreadOnly, CancellationToken cancellationToken);
    Task<NotificationInboxItemResponse> GetAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    Task<NotificationInboxItemResponse> CreateAsync(
        Guid userId,
        string sourceType,
        Guid sourceId,
        string deduplicationKey,
        string title,
        string body,
        string url,
        object? data,
        DateTime dueAtUtc,
        bool queuePush,
        CancellationToken cancellationToken);
    Task MarkReadAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationInboxItemResponse>> ListForPushGroupAsync(Guid userId, string groupKey, CancellationToken cancellationToken);
    Task PruneExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
