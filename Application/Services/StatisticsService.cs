using DoIt.Api.Application.Interfaces;
using DoIt.Api.Common;
using DoIt.Api.Contracts.Responses;
using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using DoIt.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoIt.Api.Application.Services;

public sealed class StatisticsService(DoItDbContext dbContext) : IStatisticsService
{
    public async Task<StatisticsResponse> GetAsync(Guid userId, DateOnly from, DateOnly to, string? groupBy, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "invalid_period", "The statistics period is invalid.");
        }

        if (to.DayNumber - from.DayNumber > 366)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "period_too_large", "The statistics period cannot exceed one year.");
        }

        var normalizedGroupBy = NormalizeGroupBy(groupBy);
        var user = await dbContext.Users.FirstAsync(candidate => candidate.Id == userId, cancellationToken);
        var tasks = await dbContext.Tasks
            .Include(task => task.Zone)
            .Include(task => task.Schedule)
            .Include(task => task.Assignments)
            .Where(task => !task.IsArchived &&
                ((task.Scope == TaskScope.Personal && task.CreatedByUserId == userId) ||
                 (task.Scope == TaskScope.House && (user.Role == UserRole.Admin || task.AssignmentMode == AssignmentMode.Anyone || task.Assignments.Any(assignment => assignment.UserId == userId)))))
            .OrderBy(task => task.Title)
            .ToListAsync(cancellationToken);

        var taskIds = tasks.Select(task => task.Id).ToList();
        var occurrences = await dbContext.TaskOccurrences
            .Include(occurrence => occurrence.Completions)
            .Where(occurrence => taskIds.Contains(occurrence.TaskId))
            .ToListAsync(cancellationToken);
        var occurrencesByTask = occurrences.GroupBy(occurrence => occurrence.TaskId).ToDictionary(group => group.Key, group => group.ToList());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var taskResults = new List<TaskStatisticsResponse>();

        foreach (var task in tasks)
        {
            occurrencesByTask.TryGetValue(task.Id, out var taskOccurrences);
            var occurrenceByDate = (taskOccurrences ?? [])
                .Where(occurrence => occurrence.Date >= from && occurrence.Date <= to)
                .GroupBy(occurrence => occurrence.Date)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(occurrence => occurrence.UpdatedAt).First());
            var records = new List<StatisticsOccurrenceResponse>();
            var expectedDates = GetExpectedDates(task, from, to);

            foreach (var date in expectedDates)
            {
                occurrenceByDate.TryGetValue(date, out var occurrence);
                records.Add(ToRecord(task, occurrence, date, today));
            }

            foreach (var occurrence in taskOccurrences ?? [])
            {
                if (occurrence.Date < from || occurrence.Date > to || records.Any(record => record.OccurrenceId == occurrence.Id))
                {
                    continue;
                }

                var record = ToRecord(task, occurrence, occurrence.Date, today);
                if (record.Status is "Done" or "Missed" or "NotApplicable")
                {
                    records.Add(record);
                }
            }

            var taskSummary = BuildSummary(records, task.Schedule?.RecurrenceType == RecurrenceType.TimesPerWeek ? task.Schedule.TimesPerWeek : null, task.Schedule?.StartDate, from, to, today);
            taskResults.Add(new TaskStatisticsResponse(task.Id, task.Title, task.Zone?.Name, task.Schedule?.RecurrenceType.ToString() ?? RecurrenceType.Manual.ToString(), taskSummary, records.OrderBy(record => record.ScheduledDate).ToList()));
        }

        var allRecords = taskResults.SelectMany(task => task.Occurrences).ToList();
        var summary = BuildSummary(allRecords, null, null, from, to, today);
        var buckets = BuildBuckets(allRecords, normalizedGroupBy, from, to, today);
        return new StatisticsResponse(from, to, normalizedGroupBy, summary, buckets, taskResults);
    }

    private static IReadOnlyList<DateOnly> GetExpectedDates(DoItTask task, DateOnly from, DateOnly to)
    {
        var schedule = task.Schedule;
        if (schedule is null || schedule.RecurrenceType == RecurrenceType.TimesPerWeek)
        {
            return [];
        }

        var dates = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date < schedule.StartDate || schedule.EndDate is not null && date > schedule.EndDate)
            {
                continue;
            }

            var applies = schedule.RecurrenceType switch
            {
                RecurrenceType.Manual => date == schedule.StartDate,
                RecurrenceType.Daily => true,
                RecurrenceType.Weekly => date.DayOfWeek == schedule.StartDate.DayOfWeek,
                RecurrenceType.Weekday => schedule.Weekday == date.DayOfWeek,
                RecurrenceType.EveryNDays => schedule.EveryNDays is > 0 && (date.DayNumber - schedule.StartDate.DayNumber) % schedule.EveryNDays.Value == 0,
                RecurrenceType.MonthlyOrdinalWeekday => schedule.Weekday == date.DayOfWeek && ((date.Day - 1) / 7) + 1 == schedule.WeekOfMonth,
                _ => false
            };

            if (applies)
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static StatisticsOccurrenceResponse ToRecord(DoItTask task, TaskOccurrence? occurrence, DateOnly scheduledDate, DateOnly today)
    {
        var timeZoneId = occurrence?.TimeZoneId ?? task.Schedule?.TimeZoneId ?? "UTC";
        var timeZone = FindTimeZone(timeZoneId);
        var activeCompletions = occurrence?.Completions.Where(completion => completion.RevertedAt is null).ToList() ?? [];
        var done = activeCompletions.Where(completion => completion.Action == TaskCompletionAction.Done).OrderByDescending(completion => completion.CreatedAt).FirstOrDefault();
        var latest = activeCompletions.OrderByDescending(completion => completion.CreatedAt).FirstOrDefault();

        if (done is not null)
        {
            var completedAt = DateTime.SpecifyKind(done.CreatedAt, DateTimeKind.Utc);
            var localCompletedAt = TimeZoneInfo.ConvertTimeFromUtc(completedAt, timeZone);
            var timing = "OnTime";
            int? differenceMinutes = null;
            if (localCompletedAt.Date < scheduledDate.ToDateTime(TimeOnly.MinValue).Date)
            {
                timing = "Early";
                differenceMinutes = (scheduledDate.DayNumber - DateOnly.FromDateTime(localCompletedAt).DayNumber) * 24 * 60;
            }
            else if (occurrence?.AvailableUntilAt is not null && completedAt > occurrence.AvailableUntilAt.Value)
            {
                timing = "Overdue";
                differenceMinutes = (int)Math.Round((completedAt - occurrence.AvailableUntilAt.Value).TotalMinutes);
            }
            else if (occurrence?.RecommendedAt is not null && completedAt > occurrence.RecommendedAt.Value)
            {
                timing = "Late";
                differenceMinutes = (int)Math.Round((completedAt - occurrence.RecommendedAt.Value).TotalMinutes);
            }

            return new StatisticsOccurrenceResponse(occurrence?.Id, task.Id, scheduledDate, "Done", timing, completedAt, differenceMinutes, timeZoneId);
        }

        var status = latest?.Action switch
        {
            TaskCompletionAction.NotApplicable => "NotApplicable",
            TaskCompletionAction.Missed => "Missed",
            _ when scheduledDate < today => "Missed",
            _ => "Pending"
        };
        return new StatisticsOccurrenceResponse(occurrence?.Id, task.Id, scheduledDate, status, status, null, null, timeZoneId);
    }

    private static StatisticsSummaryResponse BuildSummary(IReadOnlyList<StatisticsOccurrenceResponse> records, int? timesPerWeek, DateOnly? scheduleStart, DateOnly from, DateOnly to, DateOnly today)
    {
        var scheduled = records.Count;
        if (timesPerWeek is > 0)
        {
            var firstWeek = StartOfWeek(from);
            var lastWeek = StartOfWeek(to);
            scheduled = 0;
            for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
            {
                scheduled += timesPerWeek.Value;
            }
        }

        var completed = records.Count(record => record.Status == "Done");
        var early = records.Count(record => record.Timing == "Early");
        var late = records.Count(record => record.Timing == "Late");
        var overdue = records.Count(record => record.Timing == "Overdue");
        var notApplicable = records.Count(record => record.Status == "NotApplicable");
        var missed = records.Count(record => record.Status == "Missed");
        var pending = Math.Max(0, scheduled - completed - notApplicable - missed);
        if (timesPerWeek is > 0)
        {
            pending = Math.Max(0, scheduled - completed - missed);
            var weeklyRate = scheduled == 0 ? 0 : Math.Round((decimal)completed / scheduled * 100, 1);
            return new StatisticsSummaryResponse(scheduled, completed, early, late, overdue, missed, notApplicable, pending, weeklyRate);
        }

        var denominator = Math.Max(0, scheduled - notApplicable - pending);
        var rate = denominator == 0 ? 0 : Math.Round((decimal)completed / denominator * 100, 1);
        return new StatisticsSummaryResponse(scheduled, completed, early, late, overdue, missed, notApplicable, pending, rate);
    }

    private static IReadOnlyList<StatisticsBucketResponse> BuildBuckets(IReadOnlyList<StatisticsOccurrenceResponse> records, string groupBy, DateOnly from, DateOnly to, DateOnly today)
    {
        return records.GroupBy(record => BucketStart(record.ScheduledDate, groupBy))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var bucketFrom = group.Key;
                var bucketTo = groupBy switch
                {
                    "month" => new DateOnly(bucketFrom.Year, bucketFrom.Month, DateTime.DaysInMonth(bucketFrom.Year, bucketFrom.Month)),
                    "week" => bucketFrom.AddDays(6),
                    _ => bucketFrom
                };
                var summary = BuildSummary(group.ToList(), null, null, bucketFrom, bucketTo, today);
                return new StatisticsBucketResponse(group.Key.ToString("yyyy-MM-dd"), bucketFrom, bucketTo, summary);
            })
            .ToList();
    }

    private static DateOnly BucketStart(DateOnly date, string groupBy)
    {
        return groupBy switch
        {
            "week" => StartOfWeek(date),
            "month" => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static string NormalizeGroupBy(string? groupBy)
    {
        return groupBy?.ToLowerInvariant() switch
        {
            "week" => "week",
            "month" => "month",
            _ => "day"
        };
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
