using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("notifications")]
[Route("api/notifications")]
public sealed class NotificationsController(
    IPushSubscriptionService pushSubscriptionService,
    INotificationPreferenceService notificationPreferenceService) : ControllerBase
{
    [HttpGet("config")]
    [ProducesResponseType<WebPushConfigResponse>(StatusCodes.Status200OK)]
    public ActionResult<WebPushConfigResponse> GetConfiguration()
    {
        return Ok(pushSubscriptionService.GetPublicConfiguration());
    }

    [HttpPost("subscription")]
    [ProducesResponseType<PushSubscriptionStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PushSubscriptionStatusResponse>> UpsertSubscription(
        PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await pushSubscriptionService.UpsertAsync(GetUserId(), request, cancellationToken));
    }

    [HttpDelete("subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSubscription(
        DeletePushSubscriptionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        await pushSubscriptionService.DeactivateAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    [ProducesResponseType<NotificationPreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferenceResponse>> GetPreferences(CancellationToken cancellationToken)
    {
        return Ok(await notificationPreferenceService.GetAsync(GetUserId(), cancellationToken));
    }

    [HttpPut("preferences")]
    [ProducesResponseType<NotificationPreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferenceResponse>> UpdatePreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationPreferenceService.UpdateAsync(GetUserId(), request, cancellationToken));
    }

    [HttpGet("tasks/{taskId:guid}")]
    [ProducesResponseType<TaskNotificationOverrideResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskNotificationOverrideResponse>> GetTaskOverride(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationPreferenceService.GetTaskOverrideAsync(GetUserId(), taskId, cancellationToken));
    }

    [HttpPut("tasks/{taskId:guid}")]
    [ProducesResponseType<TaskNotificationOverrideResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskNotificationOverrideResponse>> UpdateTaskOverride(
        Guid taskId,
        UpdateTaskNotificationOverrideRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await notificationPreferenceService.UpdateTaskOverrideAsync(GetUserId(), taskId, request, cancellationToken));
    }

    [HttpDelete("tasks/{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTaskOverride(Guid taskId, CancellationToken cancellationToken)
    {
        await notificationPreferenceService.DeleteTaskOverrideAsync(GetUserId(), taskId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
