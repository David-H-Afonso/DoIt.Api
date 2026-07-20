using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;

namespace DoIt.Api.Application.Mapping;

public static class TaskMapping
{
    public static ZoneResponse ToResponse(this Zone zone)
    {
        return new ZoneResponse(
            zone.Id,
            zone.Name,
            zone.Description,
            zone.Color,
            zone.Icon,
            zone.SortOrder,
            zone.IsArchived,
            zone.CreatedAt,
            zone.UpdatedAt);
    }

    public static TaskResponse ToResponse(this DoItTask task)
    {
        return new TaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.ZoneId,
            task.Zone?.Name,
            task.Scope.ToString(),
            task.TaskType.ToString(),
            task.Importance.ToString(),
            task.Complexity.ToString(),
            task.Obligation.ToString(),
            task.AssignmentMode.ToString(),
            task.Assignments.Select(assignment => assignment.UserId).ToList(),
            task.IsArchived,
            task.CreatedByUserId,
            task.CreatedAt,
            task.UpdatedAt,
            task.Schedule?.ToResponse(),
            null,
            null,
            null,
            null);
    }

    public static TaskScheduleResponse ToResponse(this TaskSchedule schedule)
    {
        return new TaskScheduleResponse(
            schedule.Id,
            schedule.RecurrenceType.ToString(),
            schedule.StartDate,
            schedule.EndDate,
            schedule.Weekday,
            schedule.TimesPerWeek,
            schedule.EveryNDays,
            schedule.AvailableFromTime,
            schedule.AvailableUntilTime,
            schedule.RecommendedTime,
            schedule.UnavailableVisibilityMode.ToString(),
            schedule.TimeZoneId,
            schedule.CreatedAt,
            schedule.UpdatedAt);
    }
}
