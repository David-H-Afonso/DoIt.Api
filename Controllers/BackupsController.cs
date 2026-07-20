using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/backups")]
public sealed class BackupsController(IBackupService backupService) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<BackupScheduleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BackupScheduleResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await backupService.ListAsync(cancellationToken));
    }

    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType<BackupScheduleResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupScheduleResponse>> Get(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await backupService.GetAsync(userId, cancellationToken));
    }

    [HttpPut("users/{userId:guid}")]
    [ProducesResponseType<BackupScheduleResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupScheduleResponse>> Update(Guid userId, UpdateBackupScheduleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await backupService.UpdateAsync(userId, request, cancellationToken));
    }

    [HttpPost("users/{userId:guid}/run-now")]
    [ProducesResponseType<BackupScheduleResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupScheduleResponse>> RunNow(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await backupService.RunNowAsync(userId, cancellationToken));
    }

    [HttpPost("full/run-now")]
    [ProducesResponseType<FullBackupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FullBackupResponse>> RunFullNow(CancellationToken cancellationToken)
    {
        return Ok(await backupService.RunFullNowAsync(GetUserId(), cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
