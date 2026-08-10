using PGOneTrade.Models;

namespace PGOneTrade.Services;

public interface ISuperTrendService
{
    (TrendDirection direction, List<decimal> values) Calculate(List<Candle> candles, int period, double multiplier);
    TrendDirection GetTrend(List<Candle> candles, int period, double multiplier);
}

public class SuperTrendService : ISuperTrendService
{
    // Standard SuperTrend, matching TradingView's built-in ta.supertrend:
    // ATR uses Wilder/RMA smoothing; final upper/lower bands are tracked
    // separately and the trend flips only when close crosses the opposite band.
    public (TrendDirection direction, List<decimal> values) Calculate(List<Candle> candles, int period, double multiplier)
    {
        var n = candles.Count;
        if (n < period + 1)
            return (TrendDirection.Neutral, new List<decimal>());

        var factor = (decimal)multiplier;
        var atr = CalculateAtr(candles, period);

        var values = new List<decimal>();
        decimal prevUpper = 0, prevLower = 0, prevSuperTrend = 0;
        var direction = TrendDirection.Neutral;
        var started = false;

        for (int i = 0; i < n; i++)
        {
            if (atr[i] <= 0m)
                continue; // ATR not yet defined for this bar

            var hl2 = (candles[i].High + candles[i].Low) / 2m;
            var upper = hl2 + factor * atr[i];
            var lower = hl2 - factor * atr[i];
            var prevClose = candles[i - 1].Close;

            if (!started)
            {
                // Seed: TradingView starts in a downtrend when the previous ATR is NA.
                prevUpper = upper;
                prevLower = lower;
                prevSuperTrend = upper;
                direction = TrendDirection.Sell;
                values.Add(prevSuperTrend);
                started = true;
                continue;
            }

            // Carry bands forward unless price broke them.
            lower = (lower > prevLower || prevClose < prevLower) ? lower : prevLower;
            upper = (upper < prevUpper || prevClose > prevUpper) ? upper : prevUpper;

            bool isUp;
            if (prevSuperTrend == prevUpper)
                isUp = candles[i].Close > upper; // was in downtrend; flip up if close breaks upper
            else
                isUp = !(candles[i].Close < lower); // was in uptrend; flip down if close breaks lower

            var superTrend = isUp ? lower : upper;

            values.Add(superTrend);
            direction = isUp ? TrendDirection.Buy : TrendDirection.Sell;
            prevUpper = upper;
            prevLower = lower;
            prevSuperTrend = superTrend;
        }

        return (direction, values);
    }

    public TrendDirection GetTrend(List<Candle> candles, int period, double multiplier)
    {
        var (direction, _) = Calculate(candles, period, multiplier);
        return direction;
    }

    // ATR aligned to candle indices. atr[i] == 0 means "not yet defined".
    // TR[0] = high-low; ATR is seeded with the SMA of the first `period` TRs
    // then smoothed with Wilder's method (RMA), matching TradingView's ta.atr.
    private static decimal[] CalculateAtr(List<Candle> candles, int period)
    {
        var n = candles.Count;
        var tr = new decimal[n];
        tr[0] = candles[0].High - candles[0].Low;
        for (int i = 1; i < n; i++)
        {
            var highLow = candles[i].High - candles[i].Low;
            var highClose = Math.Abs(candles[i].High - candles[i - 1].Close);
            var lowClose = Math.Abs(candles[i].Low - candles[i - 1].Close);
            tr[i] = Math.Max(highLow, Math.Max(highClose, lowClose));
        }

        var atr = new decimal[n];
        if (n < period)
            return atr;

        decimal sum = 0;
        for (int i = 0; i < period; i++)
            sum += tr[i];
        atr[period - 1] = sum / period;

        for (int i = period; i < n; i++)
            atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period;

        return atr;
    }
}
