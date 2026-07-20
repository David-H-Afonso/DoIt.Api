using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NowController(INowService nowService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NowResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NowResponse>> Get([FromQuery] DateOnly? date, [FromQuery] string? scope, CancellationToken cancellationToken)
    {
        return Ok(await nowService.GetNowAsync(GetUserId(), date, scope, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
