using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/occurrences")]
public sealed class OccurrencesController(ITaskActionService taskActionService) : ControllerBase
{
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.CompleteAsync(GetUserId(), id, allowAdminOverride: true, cancellationToken));
    }

    [HttpPost("{id:guid}/complete-retroactive")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> CompleteRetroactively(Guid id, CompleteOccurrenceRetroactivelyRequest request, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.CompleteRetroactivelyAsync(GetUserId(), id, request.Date, cancellationToken));
    }

    [HttpPost("{id:guid}/complete-early")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> CompleteEarly(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.CompleteEarlyAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("{id:guid}/miss")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Miss(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.MissAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("{id:guid}/not-applicable")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> NotApplicable(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.NotApplicableAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("{id:guid}/snooze")]
    [ProducesResponseType<OccurrenceSnoozeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceSnoozeResponse>> Snooze(Guid id, SnoozeOccurrenceRequest request, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.SnoozeAsync(GetUserId(), id, request.Duration, cancellationToken));
    }

    [HttpDelete("{id:guid}/snooze")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelSnooze(Guid id, CancellationToken cancellationToken)
    {
        await taskActionService.CancelSnoozeAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Undo(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.UndoAsync(GetUserId(), id, allowAdminOverride: true, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
