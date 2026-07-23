using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DoIt.Api.Controllers;

[ApiController]
[Route("api/integrations/household")]
public sealed class HouseholdIntegrationController(IHouseholdIntegrationService integrationService) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = HouseholdIntegrationPolicies.TasksRead)]
    [ProducesResponseType<NowResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NowResponse>> Summary([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        return Ok(await integrationService.GetSummaryAsync(GetIntegrationUserId(), date, cancellationToken));
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    [Authorize(Policy = HouseholdIntegrationPolicies.TasksComplete)]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Complete(Guid taskId, CancellationToken cancellationToken)
    {
        return Ok(await integrationService.CompleteTaskAsync(GetIntegrationUserId(), taskId, cancellationToken));
    }

    [HttpPost("tasks/{taskId:guid}/undo")]
    [Authorize(Policy = HouseholdIntegrationPolicies.TasksUndo)]
    [ProducesResponseType<OccurrenceActionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OccurrenceActionResponse>> Undo(Guid taskId, CancellationToken cancellationToken)
    {
        return Ok(await integrationService.UndoTaskAsync(GetIntegrationUserId(), taskId, cancellationToken));
    }

    [HttpPost("tasks")]
    [Authorize(Policy = HouseholdIntegrationPolicies.TasksCreate)]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskResponse>> Create(HouseholdCreateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await integrationService.CreateTaskAsync(GetIntegrationUserId(), request, cancellationToken));
    }

    private Guid GetIntegrationUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) && userId != Guid.Empty
            ? userId
            : throw new InvalidOperationException("Household integration user claim is missing.");
    }
}
