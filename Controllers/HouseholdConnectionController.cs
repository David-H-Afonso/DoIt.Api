using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DoIt.Api.Controllers;

[ApiController]
[Route("api/integrations/household/v1")]
public sealed class HouseholdConnectionController(IHouseholdConnectionService connectionService) : ControllerBase
{
    [HttpPost("authorize")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("household-authorize")]
    [ProducesResponseType<HouseholdAuthorizeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HouseholdAuthorizeResponse>> Authorize(
        HouseholdAuthorizeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ReadGuidClaim(ClaimTypes.NameIdentifier);
        return Ok(await connectionService.AuthorizeAsync(userId, request, cancellationToken));
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("household-token")]
    [ProducesResponseType<HouseholdTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HouseholdTokenResponse>> Token(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await connectionService.ExchangeAsync(request, cancellationToken));

    [HttpPost("revoke")]
    [AllowAnonymous]
    [EnableRateLimiting("household-revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(HouseholdRevokeRequest request, CancellationToken cancellationToken)
    {
        await connectionService.RevokeAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = HouseholdIntegrationPolicies.ProfileRead)]
    [ProducesResponseType<HouseholdMeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HouseholdMeResponse>> Me(CancellationToken cancellationToken)
    {
        var connectionId = ReadGuidClaim("connection_id");
        return Ok(await connectionService.GetMeAsync(connectionId, cancellationToken));
    }

    private Guid ReadGuidClaim(string type)
    {
        var value = User.FindFirstValue(type);
        return Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Required authentication claim is missing.");
    }
}
