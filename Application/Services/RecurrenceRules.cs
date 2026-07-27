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

    public static DateOnly? GetNextOccurrenceDate(TaskSchedule schedule, DateOnly after)
    {
        if (schedule.RecurrenceType is RecurrenceType.Manual or RecurrenceType.TimesPerWeek || schedule.EndDate <= after)
        {
            return null;
        }

        var minimum = after.AddDays(1);
        if (minimum < schedule.StartDate)
        {
            minimum = schedule.StartDate;
        }

        var candidate = schedule.RecurrenceType switch
        {
            RecurrenceType.Daily => minimum,
            RecurrenceType.Weekly => NextWeekday(minimum, schedule.StartDate.DayOfWeek),
            RecurrenceType.Weekday when schedule.Weekday is not null => NextWeekday(minimum, schedule.Weekday.Value),
            RecurrenceType.EveryNDays when schedule.EveryNDays is > 0 => NextEveryNDays(schedule.StartDate, minimum, schedule.EveryNDays.Value),
            RecurrenceType.EveryNWeeks when schedule.Interval is > 0 => NextEveryNWeeks(schedule.StartDate, minimum, schedule.Interval.Value),
            RecurrenceType.Monthly => NextMonthlyDate(schedule.StartDate, minimum, 1),
            RecurrenceType.EveryNMonths when schedule.Interval is > 0 => NextMonthlyDate(schedule.StartDate, minimum, schedule.Interval.Value),
            RecurrenceType.EveryNYears when schedule.Interval is > 0 => NextYearlyDate(schedule.StartDate, minimum, schedule.Interval.Value),
            RecurrenceType.MonthlyOrdinalWeekday when schedule.Weekday is not null && schedule.WeekOfMonth is > 0 => NextMonthlyOrdinalWeekday(minimum, schedule.Weekday.Value, schedule.WeekOfMonth.Value),
            _ => null
        };

        return candidate is not null && (schedule.EndDate is null || candidate <= schedule.EndDate) ? candidate : null;
    }

    public static DateOnly? GetEffectiveOccurrenceDate(TaskSchedule schedule, DateOnly date)
    {
        if (AppliesOnDate(schedule, date))
        {
            return date;
        }

        if (!schedule.ExtendsUntilNextOccurrence || date < schedule.StartDate || schedule.EndDate is not null && date > schedule.EndDate)
        {
            return null;
        }

        var previous = GetPreviousOccurrenceDate(schedule, date);
        if (previous is null)
        {
            return null;
        }

        var next = GetNextOccurrenceDate(schedule, previous.Value);
        return next is not null && date < next.Value ? previous : null;
    }

    public static bool IsExpiredExtendedOccurrence(TaskSchedule schedule, DateOnly occurrenceDate, DateOnly date)
    {
        return schedule.ExtendsUntilNextOccurrence
            && GetNextOccurrenceDate(schedule, occurrenceDate) is { } next
            && date >= next;
    }

    private static DateOnly? GetPreviousOccurrenceDate(TaskSchedule schedule, DateOnly before)
    {
        var maximum = before.AddDays(-1);
        if (maximum < schedule.StartDate)
        {
            return null;
        }

        DateOnly? candidate = schedule.RecurrenceType switch
        {
            RecurrenceType.Weekly => PreviousWeekday(maximum, schedule.StartDate.DayOfWeek),
            RecurrenceType.Weekday when schedule.Weekday is not null => PreviousWeekday(maximum, schedule.Weekday.Value),
            RecurrenceType.EveryNDays when schedule.EveryNDays is > 0 => PreviousEveryNDays(schedule.StartDate, maximum, schedule.EveryNDays.Value),
            RecurrenceType.EveryNWeeks when schedule.Interval is > 0 => PreviousEveryNWeeks(schedule.StartDate, maximum, schedule.Interval.Value),
            RecurrenceType.Monthly => PreviousMonthlyDate(schedule.StartDate, maximum, 1),
            RecurrenceType.EveryNMonths when schedule.Interval is > 0 => PreviousMonthlyDate(schedule.StartDate, maximum, schedule.Interval.Value),
            RecurrenceType.EveryNYears when schedule.Interval is > 0 => PreviousYearlyDate(schedule.StartDate, maximum, schedule.Interval.Value),
            RecurrenceType.MonthlyOrdinalWeekday when schedule.Weekday is not null && schedule.WeekOfMonth is > 0 => PreviousMonthlyOrdinalWeekday(maximum, schedule.Weekday.Value, schedule.WeekOfMonth.Value),
            _ => null
        };

        return candidate is not null && candidate.Value >= schedule.StartDate ? candidate : null;
    }

    private static int DaysSince(DateOnly start, DateOnly date) => date.DayNumber - start.DayNumber;

    private static DateOnly NextWeekday(DateOnly minimum, DayOfWeek weekday)
    {
        var daysUntil = ((int)weekday - (int)minimum.DayOfWeek + 7) % 7;
        return minimum.AddDays(daysUntil);
    }

    private static DateOnly PreviousWeekday(DateOnly maximum, DayOfWeek weekday)
    {
        var daysSince = ((int)maximum.DayOfWeek - (int)weekday + 7) % 7;
        return maximum.AddDays(-daysSince);
    }

    private static DateOnly NextEveryNDays(DateOnly start, DateOnly minimum, int interval)
    {
        var daysSinceStart = DaysSince(start, minimum);
        var daysUntil = (interval - daysSinceStart % interval) % interval;
        return minimum.AddDays(daysUntil);
    }

    private static DateOnly PreviousEveryNDays(DateOnly start, DateOnly maximum, int interval)
    {
        var daysSinceStart = DaysSince(start, maximum);
        return start.AddDays(daysSinceStart - daysSinceStart % interval);
    }

    private static DateOnly NextEveryNWeeks(DateOnly start, DateOnly minimum, int interval)
    {
        var weeksSinceStart = Math.Max(0, DaysSince(start, minimum) / 7);
        var alignedWeeks = (weeksSinceStart + interval - 1) / interval * interval;
        var candidate = start.AddDays(alignedWeeks * 7);
        return candidate < minimum ? candidate.AddDays(interval * 7) : candidate;
    }

    private static DateOnly PreviousEveryNWeeks(DateOnly start, DateOnly maximum, int interval)
    {
        var weeksSinceStart = Math.Max(0, DaysSince(start, maximum) / 7);
        var alignedWeeks = weeksSinceStart / interval * interval;
        var candidate = start.AddDays(alignedWeeks * 7);
        return candidate > maximum ? candidate.AddDays(-interval * 7) : candidate;
    }

    private static DateOnly NextMonthlyDate(DateOnly start, DateOnly minimum, int interval)
    {
        var monthsSinceStart = (minimum.Year - start.Year) * 12 + minimum.Month - start.Month;
        var alignedMonths = Math.Max(0, (monthsSinceStart + interval - 1) / interval * interval);
        var candidate = CreateMonthlyDate(start, alignedMonths);
        if (candidate < minimum)
        {
            candidate = CreateMonthlyDate(start, alignedMonths + interval);
        }

        return candidate;
    }

    private static DateOnly PreviousMonthlyDate(DateOnly start, DateOnly maximum, int interval)
    {
        var monthsSinceStart = (maximum.Year - start.Year) * 12 + maximum.Month - start.Month;
        var alignedMonths = Math.Max(0, monthsSinceStart / interval * interval);
        var candidate = CreateMonthlyDate(start, alignedMonths);
        if (candidate > maximum)
        {
            candidate = CreateMonthlyDate(start, alignedMonths - interval);
        }

        return candidate;
    }

    private static DateOnly NextYearlyDate(DateOnly start, DateOnly minimum, int interval)
    {
        var yearsSinceStart = minimum.Year - start.Year;
        var alignedYears = Math.Max(0, (yearsSinceStart + interval - 1) / interval * interval);
        var candidate = CreateYearlyDate(start, alignedYears);
        if (candidate < minimum)
        {
            candidate = CreateYearlyDate(start, alignedYears + interval);
        }

        return candidate;
    }

    private static DateOnly PreviousYearlyDate(DateOnly start, DateOnly maximum, int interval)
    {
        var yearsSinceStart = maximum.Year - start.Year;
        var alignedYears = Math.Max(0, yearsSinceStart / interval * interval);
        var candidate = CreateYearlyDate(start, alignedYears);
        if (candidate > maximum)
        {
            candidate = CreateYearlyDate(start, alignedYears - interval);
        }

        return candidate;
    }

    private static DateOnly CreateMonthlyDate(DateOnly start, int monthsSinceStart)
    {
        var month = start.AddMonths(monthsSinceStart);
        return new DateOnly(month.Year, month.Month, Math.Min(start.Day, DateTime.DaysInMonth(month.Year, month.Month)));
    }

    private static DateOnly CreateYearlyDate(DateOnly start, int yearsSinceStart)
    {
        var year = start.Year + yearsSinceStart;
        return new DateOnly(year, start.Month, Math.Min(start.Day, DateTime.DaysInMonth(year, start.Month)));
    }

    private static DateOnly? NextMonthlyOrdinalWeekday(DateOnly minimum, DayOfWeek weekday, int weekOfMonth)
    {
        var month = new DateOnly(minimum.Year, minimum.Month, 1);
        for (var monthOffset = 0; monthOffset <= 1200; monthOffset++)
        {
            var candidateMonth = month.AddMonths(monthOffset);
            var daysUntilWeekday = ((int)weekday - (int)candidateMonth.DayOfWeek + 7) % 7;
            var day = 1 + daysUntilWeekday + (weekOfMonth - 1) * 7;
            if (day <= DateTime.DaysInMonth(candidateMonth.Year, candidateMonth.Month))
            {
                var candidate = new DateOnly(candidateMonth.Year, candidateMonth.Month, day);
                if (candidate >= minimum)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static DateOnly? PreviousMonthlyOrdinalWeekday(DateOnly maximum, DayOfWeek weekday, int weekOfMonth)
    {
        for (var monthOffset = 0; monthOffset <= 1200; monthOffset++)
        {
            var candidateMonth = new DateOnly(maximum.Year, maximum.Month, 1).AddMonths(-monthOffset);
            var daysUntilWeekday = ((int)weekday - (int)candidateMonth.DayOfWeek + 7) % 7;
            var day = 1 + daysUntilWeekday + (weekOfMonth - 1) * 7;
            if (day > DateTime.DaysInMonth(candidateMonth.Year, candidateMonth.Month))
            {
                continue;
            }

            var candidate = new DateOnly(candidateMonth.Year, candidateMonth.Month, day);
            if (candidate <= maximum)
            {
                return candidate;
            }
        }

        return null;
    }

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
