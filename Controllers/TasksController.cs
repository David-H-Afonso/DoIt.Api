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
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await taskService.ListAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await taskService.GetAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await taskService.CreateAsync(GetUserId(), request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskResponse>> Update(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await taskService.UpdateAsync(GetUserId(), id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await taskService.ArchiveAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await taskService.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        await taskService.RestoreAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
