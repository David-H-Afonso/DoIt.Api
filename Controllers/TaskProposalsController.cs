using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tasks/proposals")]
[Route("api/task-proposals")]
public sealed class TaskProposalsController(ITaskProposalService proposalService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskProposalResponse>> Create(CreateTaskProposalRequest request, CancellationToken cancellationToken)
        => Ok(await proposalService.CreateAsync(GetUserId(), request, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskProposalResponse>>> List(CancellationToken cancellationToken)
        => Ok(await proposalService.ListAsync(GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskProposalResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await proposalService.GetAsync(GetUserId(), id, cancellationToken));

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<TaskProposalResponse>> Accept(Guid id, CancellationToken cancellationToken)
        => Ok(await proposalService.AcceptAsync(GetUserId(), id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TaskProposalResponse>> Reject(Guid id, CancellationToken cancellationToken)
        => Ok(await proposalService.RejectAsync(GetUserId(), id, cancellationToken));

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : throw new UnauthorizedAccessException();
}
