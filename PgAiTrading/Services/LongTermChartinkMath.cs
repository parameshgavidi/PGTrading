using PgAiTrading.Models;

namespace PgAiTrading.Services;

/// <summary>Shared Chartink-parity math for the long-term framework.</summary>
public static class LongTermChartinkMath
{
    /// <summary>Trading days in ~1 year — Chartink "Yearly High" lookback.</summary>
    public const int YearlyHighLookbackDays = 252;

    /// <summary>Max high over the last <paramref name="lookbackDays"/> daily candles.</summary>
    public static decimal YearlyHigh(IReadOnlyList<Candle> daily, int lookbackDays = YearlyHighLookbackDays)
    {
        if (daily.Count == 0)
            return 0m;

        var count = Math.Min(lookbackDays, daily.Count);
        var high = 0m;
        for (var i = daily.Count - count; i < daily.Count; i++)
        {
            if (daily[i].High > high)
                high = daily[i].High;
        }

        return high;
    }
}
