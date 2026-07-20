using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ReviewController(IReviewService reviewService) : ControllerBase
{
    [HttpGet("today")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<ReviewResponse>> Today(CancellationToken cancellationToken)
    {
        return Get(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
    }

    [HttpGet("{date}")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewResponse>> Get(DateOnly date, CancellationToken cancellationToken)
    {
        return Ok(await reviewService.GetReviewAsync(GetUserId(), date, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
