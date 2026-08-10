namespace PGOneTrade.Services;

public static class MarketHours
{
    public static readonly TimeSpan OpenTime = new(9, 15, 0);
    public static readonly TimeSpan CloseTime = new(15, 30, 0);

    public static DateTime GetIstNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
    }

    public static bool IsOpen(DateTime? istNow = null)
    {
        var now = istNow ?? GetIstNow();
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        return now.TimeOfDay >= OpenTime && now.TimeOfDay <= CloseTime;
    }

    public static DateTime GetLastSessionClose(DateTime istNow)
    {
        var close = istNow.Date.Add(CloseTime);
        if (istNow.DayOfWeek is DayOfWeek.Saturday)
            close = close.AddDays(-1);
        else if (istNow.DayOfWeek is DayOfWeek.Sunday)
            close = close.AddDays(-2);
        else if (istNow.TimeOfDay < OpenTime)
            close = close.AddDays(istNow.DayOfWeek == DayOfWeek.Monday ? -3 : -1);

        return close;
    }

    /// <summary>
    /// Trading session date for chart overlays (1m CPR bands, session candle filter).
    /// Uses the last completed/open session — not calendar "today" on weekends/holidays.
    /// </summary>
    public static DateTime GetChartSessionDate(DateTime? istNow = null)
    {
        var now = istNow ?? GetIstNow();
        return GetLastSessionClose(now).Date;
    }
}
