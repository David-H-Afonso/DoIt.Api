using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/xp")]
public sealed class XpController(IXpService xpService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserXpResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserXpResponse>> Me(CancellationToken cancellationToken)
    {
        return Ok(await xpService.GetUserXpAsync(GetUserId(), cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
