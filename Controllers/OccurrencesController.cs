using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
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
        return Ok(await taskActionService.CompleteAsync(GetUserId(), id, cancellationToken));
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

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Undo(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskActionService.UndoAsync(GetUserId(), id, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
