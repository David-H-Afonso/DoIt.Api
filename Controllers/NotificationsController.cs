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
public sealed class NotificationsController(IPushSubscriptionService pushSubscriptionService) : ControllerBase
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

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
