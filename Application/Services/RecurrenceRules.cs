using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;

namespace DoIt.Api.Application.Services;

public static class RecurrenceRules
{
    public static bool AppliesOnDate(TaskSchedule schedule, DateOnly date)
    {
        if (date < schedule.StartDate || schedule.EndDate is not null && date > schedule.EndDate)
        {
            return false;
        }

        return schedule.RecurrenceType switch
        {
            RecurrenceType.Manual => true,
            RecurrenceType.Daily => true,
            RecurrenceType.Weekly => date.DayOfWeek == schedule.StartDate.DayOfWeek,
            RecurrenceType.Weekday => schedule.Weekday == date.DayOfWeek,
            RecurrenceType.TimesPerWeek => true,
            RecurrenceType.EveryNDays => schedule.EveryNDays is > 0 && DaysSince(schedule.StartDate, date) % schedule.EveryNDays.Value == 0,
            RecurrenceType.EveryNWeeks => schedule.Interval is > 0 && date.DayOfWeek == schedule.StartDate.DayOfWeek && DaysSince(schedule.StartDate, date) / 7 % schedule.Interval.Value == 0,
            RecurrenceType.Monthly => IsMonthlyDate(schedule.StartDate, date, 1),
            RecurrenceType.EveryNMonths => IsMonthlyDate(schedule.StartDate, date, schedule.Interval),
            RecurrenceType.EveryNYears => IsYearlyDate(schedule.StartDate, date, schedule.Interval),
            RecurrenceType.MonthlyOrdinalWeekday => schedule.Weekday == date.DayOfWeek && ((date.Day - 1) / 7) + 1 == schedule.WeekOfMonth,
            _ => false
        };
    }

    public static IReadOnlyList<DateOnly> GetExpectedDates(TaskSchedule? schedule, DateOnly from, DateOnly to)
    {
        if (schedule is null || schedule.RecurrenceType == RecurrenceType.TimesPerWeek || to < from)
        {
            return [];
        }

        var dates = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (AppliesOnDate(schedule, date))
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static int DaysSince(DateOnly start, DateOnly date) => date.DayNumber - start.DayNumber;

    private static bool IsMonthlyDate(DateOnly start, DateOnly date, int? interval)
    {
        if (interval is not > 0)
        {
            return false;
        }

        var months = (date.Year - start.Year) * 12 + date.Month - start.Month;
        var effectiveDay = Math.Min(start.Day, DateTime.DaysInMonth(date.Year, date.Month));
        return months >= 0 && months % interval.Value == 0 && date.Day == effectiveDay;
    }

    private static bool IsYearlyDate(DateOnly start, DateOnly date, int? interval)
    {
        if (interval is not > 0)
        {
            return false;
        }

        var years = date.Year - start.Year;
        var effectiveDay = Math.Min(start.Day, DateTime.DaysInMonth(date.Year, start.Month));
        return years >= 0 && years % interval.Value == 0 && date.Month == start.Month && date.Day == effectiveDay;
    }
}
