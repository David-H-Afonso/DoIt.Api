using DoIt.Api.Contracts.Requests;
using DoIt.Api.Contracts.Responses;

namespace DoIt.Api.Application.Interfaces;

public interface ICalendarEventService
{
    Task<IReadOnlyList<CalendarEventResponse>> ListAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);
    Task<CalendarEventResponse> GetAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task<CalendarEventResponse> CreateAsync(Guid userId, CreateCalendarEventRequest request, CancellationToken cancellationToken);
    Task<CalendarEventResponse> UpdateAsync(Guid userId, Guid eventId, UpdateCalendarEventRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarReminderDueResponse>> GetDueRemindersAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task AcknowledgeReminderAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);
    Task<CalendarMonthlyReportResponse> GetMonthlyReportAsync(Guid userId, int year, int month, string? timeZoneId, CancellationToken cancellationToken);
}
