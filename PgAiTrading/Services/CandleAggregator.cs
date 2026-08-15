using PgAiTrading.Models;

namespace PgAiTrading.Services;

public static class CandleAggregator
{
    public static List<Candle> ToWeekly(List<Candle> daily)
    {
        if (daily.Count == 0)
            return new List<Candle>();

        return daily
            .GroupBy(c => GetWeekStart(c.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.Timestamp).ToList();
                return new Candle
                {
                    Timestamp = g.Key,
                    Open = ordered.First().Open,
                    High = ordered.Max(c => c.High),
                    Low = ordered.Min(c => c.Low),
                    Close = ordered.Last().Close,
                    Volume = ordered.Sum(c => c.Volume)
                };
            })
            .ToList();
    }

    public static List<Candle> ToMonthly(IReadOnlyList<Candle> daily)
    {
        if (daily.Count == 0)
            return new List<Candle>();

        return daily
            .GroupBy(c => new DateTime(c.Timestamp.Year, c.Timestamp.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.Timestamp).ToList();
                return new Candle
                {
                    Timestamp = g.Key,
                    Open = ordered.First().Open,
                    High = ordered.Max(c => c.High),
                    Low = ordered.Min(c => c.Low),
                    Close = ordered.Last().Close,
                    Volume = ordered.Sum(c => c.Volume)
                };
            })
            .ToList();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
