using PGOne.Models;

namespace PGOne.Services;

public interface ISuperTrendService
{
    (TrendDirection direction, List<decimal> values) Calculate(List<Candle> candles, int period, double multiplier);
    TrendDirection GetTrend(List<Candle> candles, int period, double multiplier);
}

public class SuperTrendService : ISuperTrendService
{
    public (TrendDirection direction, List<decimal> values) Calculate(List<Candle> candles, int period, double multiplier)
    {
        if (candles.Count < period + 1)
            return (TrendDirection.Neutral, new List<decimal>());

        var atr = CalculateAtr(candles, period);
        var superTrend = new List<decimal>();
        var direction = TrendDirection.Neutral;

        decimal upperBand = 0, lowerBand = 0, prevSuperTrend = 0;
        TrendDirection prevDirection = TrendDirection.Neutral;

        for (int i = period; i < candles.Count; i++)
        {
            var hl2 = (candles[i].High + candles[i].Low) / 2;
            upperBand = hl2 + (decimal)(multiplier * (double)atr[i - period]);
            lowerBand = hl2 - (decimal)(multiplier * (double)atr[i - period]);

            if (i == period)
            {
                prevSuperTrend = upperBand;
                prevDirection = candles[i].Close > prevSuperTrend ? TrendDirection.Buy : TrendDirection.Sell;
            }
            else
            {
                if (prevDirection == TrendDirection.Buy)
                {
                    lowerBand = Math.Max(lowerBand, prevSuperTrend);
                    if (candles[i].Close < lowerBand)
                    {
                        prevDirection = TrendDirection.Sell;
                        prevSuperTrend = upperBand;
                    }
                    else
                    {
                        prevSuperTrend = lowerBand;
                    }
                }
                else
                {
                    upperBand = Math.Min(upperBand, prevSuperTrend);
                    if (candles[i].Close > upperBand)
                    {
                        prevDirection = TrendDirection.Buy;
                        prevSuperTrend = lowerBand;
                    }
                    else
                    {
                        prevSuperTrend = upperBand;
                    }
                }
            }

            superTrend.Add(prevSuperTrend);
            direction = prevDirection;
        }

        return (direction, superTrend);
    }

    public TrendDirection GetTrend(List<Candle> candles, int period, double multiplier)
    {
        var (direction, _) = Calculate(candles, period, multiplier);
        return direction;
    }

    private static List<decimal> CalculateAtr(List<Candle> candles, int period)
    {
        var tr = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var highLow = candles[i].High - candles[i].Low;
            var highClose = Math.Abs(candles[i].High - candles[i - 1].Close);
            var lowClose = Math.Abs(candles[i].Low - candles[i - 1].Close);
            tr.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
        }

        var atr = new List<decimal>();
        decimal sum = 0;
        for (int i = 0; i < tr.Count; i++)
        {
            if (i < period)
            {
                sum += tr[i];
                if (i == period - 1)
                    atr.Add(sum / period);
            }
            else
            {
                var prev = atr[^1];
                atr.Add((prev * (period - 1) + tr[i]) / period);
            }
        }

        return atr;
    }
}
