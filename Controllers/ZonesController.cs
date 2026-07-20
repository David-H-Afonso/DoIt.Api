using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ZonesController(IZoneService zoneService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ZoneResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ZoneResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await zoneService.ListAsync(GetUserId(), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<ZoneResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ZoneResponse>> Create(CreateZoneRequest request, CancellationToken cancellationToken)
    {
        return Ok(await zoneService.CreateAsync(GetUserId(), request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ZoneResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ZoneResponse>> Update(Guid id, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        return Ok(await zoneService.UpdateAsync(GetUserId(), id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await zoneService.ArchiveAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
