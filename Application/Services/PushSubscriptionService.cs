using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Configuration;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoIt.Api.Application.Services;

public sealed class PushSubscriptionService(
    DoItDbContext dbContext,
    IOptions<WebPushSettings> webPushOptions,
    TimeProvider timeProvider) : IPushSubscriptionService
{
    private const int EndpointMaxLength = 2048;
    private const int KeyMaxLength = 512;
    private const int DeviceNameMaxLength = 200;

    private readonly WebPushSettings _settings = webPushOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public WebPushConfigResponse GetPublicConfiguration()
    {
        var enabled = _settings.Enabled && _settings.HasVapidConfiguration;
        return new WebPushConfigResponse(enabled, enabled ? _settings.PublicKey.Trim() : null);
    }

    public async Task<PushSubscriptionStatusResponse> UpsertAsync(
        Guid userId,
        PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = ValidateEndpoint(request.Endpoint);
        var p256dh = ValidateKey(request.P256dh, "p256dh");
        var auth = ValidateKey(request.Auth, "auth");
        var deviceName = NormalizeDeviceName(request.DeviceName);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var subscription = await dbContext.PushSubscriptions
            .FirstOrDefaultAsync(candidate => candidate.Endpoint == endpoint, cancellationToken);

        if (subscription is not null && subscription.UserId != userId)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "push_subscription_owned", "Push subscription belongs to another user.");
        }

        if (subscription is null)
        {
            subscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                DeviceName = deviceName,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastSeenAt = now
            };
            dbContext.PushSubscriptions.Add(subscription);
        }
        else
        {
            subscription.P256dh = p256dh;
            subscription.Auth = auth;
            subscription.DeviceName = deviceName;
            subscription.IsActive = true;
            subscription.UpdatedAt = now;
            subscription.LastSeenAt = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "push_subscription_conflict", "Push subscription could not be registered.");
        }

        return ToResponse(subscription);
    }

    public async Task DeactivateAsync(
        Guid userId,
        DeletePushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var hasEndpoint = !string.IsNullOrWhiteSpace(request.Endpoint);
        if (hasEndpoint == (request.Id is not null))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "push_subscription_target_required", "Provide exactly one subscription endpoint or id.");
        }

        PushSubscription? subscription;
        if (hasEndpoint)
        {
            var endpoint = ValidateEndpoint(request.Endpoint!);
            subscription = await dbContext.PushSubscriptions
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Endpoint == endpoint, cancellationToken);
        }
        else
        {
            subscription = await dbContext.PushSubscriptions
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == request.Id, cancellationToken);
        }

        if (subscription is null)
        {
            return;
        }

        subscription.IsActive = false;
        subscription.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "push_endpoint_required", "Push subscription endpoint is required.");
        }

        var normalized = endpoint.Trim();
        if (normalized.Length > EndpointMaxLength
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_push_endpoint", "Push subscription endpoint must be a valid HTTPS URL of 2048 characters or fewer.");
        }

        return normalized;
    }

    private static string ValidateKey(string value, string keyName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > KeyMaxLength)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, $"invalid_push_{keyName}", $"Push subscription {keyName} is required and must be {KeyMaxLength} characters or fewer.");
        }

        return value.Trim();
    }

    private static string? NormalizeDeviceName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > DeviceNameMaxLength)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_push_device_name", $"Device name must be {DeviceNameMaxLength} characters or fewer.");
        }

        return normalized;
    }

    private static PushSubscriptionStatusResponse ToResponse(PushSubscription subscription) => new(
        subscription.Id,
        subscription.Endpoint,
        subscription.DeviceName,
        subscription.IsActive,
        subscription.CreatedAt,
        subscription.UpdatedAt,
        subscription.LastSeenAt);
}
