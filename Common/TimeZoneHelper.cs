namespace DoIt.Api.Common;

public static class TimeZoneHelper
{
    public static string Normalize(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return "UTC";
        }

        var candidate = timeZoneId.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(candidate);
            return candidate;
        }
        catch (TimeZoneNotFoundException)
        {
            return HasConvertibleId(candidate) ? candidate : "UTC";
        }
        catch (InvalidTimeZoneException)
        {
            return HasConvertibleId(candidate) ? candidate : "UTC";
        }
    }

    public static TimeZoneInfo Find(string? timeZoneId)
    {
        var normalized = Normalize(timeZoneId);
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalized, out var windowsId) && windowsId is not null)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out var ianaId) && ianaId is not null)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }

            return TimeZoneInfo.Utc;
        }
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly? time, string? timeZoneId)
    {
        var local = date.ToDateTime(time ?? TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Find(timeZoneId));
    }

    private static bool HasConvertibleId(string timeZoneId)
    {
        return TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _)
            || TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out _);
    }
}
