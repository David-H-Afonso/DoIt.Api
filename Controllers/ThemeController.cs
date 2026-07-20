using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/theme")]
public sealed class ThemeController(IThemePreferenceService themePreferenceService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ThemePreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ThemePreferenceResponse>> Me(CancellationToken cancellationToken)
    {
        return Ok(await themePreferenceService.GetAsync(GetUserId(), cancellationToken));
    }

    [HttpPut("me")]
    [ProducesResponseType<ThemePreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ThemePreferenceResponse>> Update(UpdateThemePreferenceRequest request, CancellationToken cancellationToken)
    {
        return Ok(await themePreferenceService.UpdateAsync(GetUserId(), request, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
