using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class CalendarEventService(DoItDbContext dbContext) : ICalendarEventService
{
    public async Task<IReadOnlyList<CalendarEventResponse>> ListAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var query = QueryUserEvents(userId);
        if (from is not null)
        {
            query = query.Where(calendarEvent => calendarEvent.EndAtUtc >= from.Value.UtcDateTime);
        }

        if (to is not null)
        {
            query = query.Where(calendarEvent => calendarEvent.StartAtUtc <= to.Value.UtcDateTime);
        }

        var events = await query.OrderBy(calendarEvent => calendarEvent.StartAtUtc).ThenBy(calendarEvent => calendarEvent.Title).ToListAsync(cancellationToken);
        return events.Select(ToResponse).ToList();
    }

    public async Task<CalendarEventResponse> GetAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var calendarEvent = await QueryUserEvents(userId).FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        return calendarEvent is null
            ? throw new ApiException(StatusCodes.Status404NotFound, "calendar_event_not_found", "Calendar event not found.")
            : ToResponse(calendarEvent);
    }

    public async Task<CalendarEventResponse> CreateAsync(Guid userId, CreateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var reminders = ValidateRequest(request.Title, request.Description, request.StartAt, request.EndAt, request.TimeZoneId, request.Reminders);
        await ValidateZoneAsync(userId, request.ZoneId, cancellationToken);
        var now = DateTime.UtcNow;
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            ZoneId = request.ZoneId,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            StartAtUtc = request.StartAt.UtcDateTime,
            EndAtUtc = request.EndAt.UtcDateTime,
            IsAllDay = request.IsAllDay,
            TimeZoneId = TimeZoneHelper.Normalize(request.TimeZoneId),
            CreatedAt = now,
            UpdatedAt = now
        };
        AddReminders(calendarEvent, reminders, now);
        dbContext.CalendarEvents.Add(calendarEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, calendarEvent.Id, cancellationToken);
    }

    public async Task<CalendarEventResponse> UpdateAsync(Guid userId, Guid eventId, UpdateCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var reminders = ValidateRequest(request.Title, request.Description, request.StartAt, request.EndAt, request.TimeZoneId, request.Reminders);
        var calendarEvent = await QueryUserEvents(userId).FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "calendar_event_not_found", "Calendar event not found.");
        }

        await ValidateZoneAsync(userId, request.ZoneId, cancellationToken);
        var now = DateTime.UtcNow;
        calendarEvent.ZoneId = request.ZoneId;
        calendarEvent.Title = request.Title.Trim();
        calendarEvent.Description = NormalizeOptional(request.Description);
        calendarEvent.StartAtUtc = request.StartAt.UtcDateTime;
        calendarEvent.EndAtUtc = request.EndAt.UtcDateTime;
        calendarEvent.IsAllDay = request.IsAllDay;
        calendarEvent.TimeZoneId = TimeZoneHelper.Normalize(request.TimeZoneId);
        calendarEvent.IsCancelled = request.IsCancelled;
        calendarEvent.UpdatedAt = now;
        dbContext.CalendarEventReminders.RemoveRange(calendarEvent.Reminders);
        calendarEvent.Reminders.Clear();
        AddReminders(calendarEvent, reminders, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, eventId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var calendarEvent = await QueryUserEvents(userId).FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return;
        }

        dbContext.CalendarEvents.Remove(calendarEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarReminderDueResponse>> GetDueRemindersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_reminder_range", "Reminder range is invalid.");
        }

        var events = await QueryUserEvents(userId)
            .Where(calendarEvent => calendarEvent.StartAtUtc >= from.UtcDateTime.AddMinutes(-1) && calendarEvent.StartAtUtc <= to.UtcDateTime.AddDays(1))
            .ToListAsync(cancellationToken);
        return events
            .SelectMany(calendarEvent => calendarEvent.Reminders
                .Where(reminder => reminder.IsEnabled && reminder.AcknowledgedAt is null)
                .Select(reminder => new CalendarReminderDueResponse(
                    reminder.Id,
                    calendarEvent.Id,
                    calendarEvent.Title,
                    calendarEvent.StartAtUtc,
                    reminder.OffsetMinutes,
                    calendarEvent.StartAtUtc.AddMinutes(-reminder.OffsetMinutes))))
            .Where(reminder => reminder.DueAtUtc >= from.UtcDateTime && reminder.DueAtUtc <= to.UtcDateTime)
            .OrderBy(reminder => reminder.DueAtUtc)
            .ToList();
    }

    public async Task AcknowledgeReminderAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.CalendarEventReminders
            .Include(candidate => candidate.CalendarEvent)
            .FirstOrDefaultAsync(candidate => candidate.Id == reminderId && candidate.CalendarEvent!.CreatedByUserId == userId, cancellationToken);
        if (reminder is null)
        {
            throw new ApiException(StatusCodes.Status404NotFound, "calendar_reminder_not_found", "Calendar reminder not found.");
        }

        reminder.AcknowledgedAt = DateTime.UtcNow;
        reminder.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CalendarMonthlyReportResponse> GetMonthlyReportAsync(Guid userId, int year, int month, string? timeZoneId, CancellationToken cancellationToken)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_month", "Year or month is invalid.");
        }

        var zone = TimeZoneHelper.Find(timeZoneId);
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddMonths(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        var events = await QueryUserEvents(userId)
            .Where(calendarEvent => calendarEvent.StartAtUtc < endUtc && calendarEvent.EndAtUtc >= startUtc)
            .ToListAsync(cancellationToken);
        var reminders = events.SelectMany(calendarEvent => calendarEvent.Reminders).ToList();
        return new CalendarMonthlyReportResponse(
            year,
            month,
            events.Count,
            events.Count(calendarEvent => !calendarEvent.IsCancelled),
            events.Count(calendarEvent => calendarEvent.IsCancelled),
            reminders.Count(reminder => reminder.IsEnabled),
            reminders.Count(reminder => reminder.AcknowledgedAt is not null));
    }

    private IQueryable<CalendarEvent> QueryUserEvents(Guid userId)
    {
        return dbContext.CalendarEvents
            .Include(calendarEvent => calendarEvent.Zone)
            .Include(calendarEvent => calendarEvent.Reminders)
            .Where(calendarEvent => calendarEvent.CreatedByUserId == userId);
    }

    private async Task ValidateZoneAsync(Guid userId, Guid? zoneId, CancellationToken cancellationToken)
    {
        if (zoneId is null)
        {
            return;
        }

        if (!await dbContext.Zones.AnyAsync(zone => zone.Id == zoneId && zone.CreatedByUserId == userId && !zone.IsArchived, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_zone", "Zone does not exist.");
        }
    }

    private static IReadOnlyList<CalendarReminderRequest> ValidateRequest(
        string title,
        string? description,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string? timeZoneId,
        IReadOnlyList<CalendarReminderRequest>? reminders)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 220)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_calendar_title", "Calendar title is required and must be 220 characters or fewer.");
        }

        if (description?.Length > 2000)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_calendar_description", "Calendar description must be 2000 characters or fewer.");
        }

        if (endAt <= startAt)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_calendar_range", "Calendar event end must be after its start.");
        }

        var normalizedTimeZone = TimeZoneHelper.Normalize(timeZoneId);
        if (!string.IsNullOrWhiteSpace(timeZoneId) && normalizedTimeZone == "UTC" && !timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_time_zone", "Time zone is invalid.");
        }

        var normalizedReminders = (reminders ?? Array.Empty<CalendarReminderRequest>()).ToList();
        if (normalizedReminders.Any(reminder => reminder.OffsetMinutes < 0))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_reminder_offset", "Reminder offsets must be zero or greater.");
        }

        if (normalizedReminders.GroupBy(reminder => reminder.OffsetMinutes).Any(group => group.Count() > 1))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "duplicate_reminder", "Reminder offsets must be unique.");
        }

        return normalizedReminders;
    }

    private static void AddReminders(CalendarEvent calendarEvent, IEnumerable<CalendarReminderRequest> reminders, DateTime now)
    {
        foreach (var reminder in reminders)
        {
            calendarEvent.Reminders.Add(new CalendarEventReminder
            {
                Id = Guid.NewGuid(),
                CalendarEventId = calendarEvent.Id,
                OffsetMinutes = reminder.OffsetMinutes,
                IsEnabled = reminder.IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static CalendarEventResponse ToResponse(CalendarEvent calendarEvent) => new(
        calendarEvent.Id,
        calendarEvent.Title,
        calendarEvent.Description,
        calendarEvent.ZoneId,
        calendarEvent.Zone?.Name,
        calendarEvent.StartAtUtc,
        calendarEvent.EndAtUtc,
        calendarEvent.IsAllDay,
        calendarEvent.TimeZoneId,
        calendarEvent.IsCancelled,
        calendarEvent.CreatedAt,
        calendarEvent.UpdatedAt,
        calendarEvent.Reminders
            .OrderBy(reminder => reminder.OffsetMinutes)
            .Select(reminder => new CalendarReminderResponse(
                reminder.Id,
                reminder.OffsetMinutes,
                reminder.IsEnabled,
                reminder.AcknowledgedAt,
                calendarEvent.StartAtUtc.AddMinutes(-reminder.OffsetMinutes)))
            .ToList());

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
