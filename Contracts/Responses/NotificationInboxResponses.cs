namespace DoIt.Api.Contracts.Responses;

public sealed record NotificationInboxItemResponse(
    Guid Id,
    string SourceType,
    Guid SourceId,
    string Title,
    string Body,
    string Url,
    object? Data,
    DateTime DueAtUtc,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAtUtc);
