using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statistics")]
public sealed class StatisticsController(IStatisticsService statisticsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<StatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StatisticsResponse>> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? groupBy, CancellationToken cancellationToken)
    {
        return Ok(await statisticsService.GetAsync(GetUserId(), from, to, groupBy, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
