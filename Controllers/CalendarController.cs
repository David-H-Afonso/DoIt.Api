using System.Security.Claims;
using DoIt.Api.Application.Interfaces;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar")]
public sealed class CalendarController(ICalendarEventService calendarEventService) : ControllerBase
{
    [HttpGet("events")]
    [ProducesResponseType<IReadOnlyList<CalendarEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarEventResponse>>> List(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.ListAsync(GetUserId(), from, to, cancellationToken));
    }

    [HttpGet("events/{id:guid}")]
    [ProducesResponseType<CalendarEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarEventResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.GetAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("events")]
    [ProducesResponseType<CalendarEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarEventResponse>> Create(CreateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.CreateAsync(GetUserId(), request, cancellationToken));
    }

    [HttpPut("events/{id:guid}")]
    [ProducesResponseType<CalendarEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarEventResponse>> Update(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.UpdateAsync(GetUserId(), id, request, cancellationToken));
    }

    [HttpDelete("events/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await calendarEventService.DeleteAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("reminders/due")]
    [ProducesResponseType<IReadOnlyList<CalendarReminderDueResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarReminderDueResponse>>> DueReminders(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.GetDueRemindersAsync(GetUserId(), from, to, cancellationToken));
    }

    [HttpPost("reminders/{id:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcknowledgeReminder(Guid id, CancellationToken cancellationToken)
    {
        await calendarEventService.AcknowledgeReminderAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("reports/monthly")]
    [ProducesResponseType<CalendarMonthlyReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarMonthlyReportResponse>> MonthlyReport(int year, int month, string? timeZoneId, CancellationToken cancellationToken)
    {
        return Ok(await calendarEventService.GetMonthlyReportAsync(GetUserId(), year, month, timeZoneId, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}
