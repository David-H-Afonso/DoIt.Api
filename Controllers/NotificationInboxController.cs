using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications/inbox")]
public sealed class NotificationInboxController(INotificationInboxService inboxService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationInboxItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationInboxItemResponse>>> List([FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default)
        => Ok(await inboxService.ListAsync(GetUserId(), unreadOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationInboxItemResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await inboxService.GetAsync(GetUserId(), id, cancellationToken));

    [HttpPost("{id:guid}/read")]
    [HttpPatch("{id:guid}/read")]
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await inboxService.MarkReadAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await inboxService.MarkAllReadAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : throw new UnauthorizedAccessException();
}
