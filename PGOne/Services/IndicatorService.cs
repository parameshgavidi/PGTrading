using PGOne.Models;

namespace PGOne.Services;

public interface IIndicatorService
{
    decimal CalculateRsi(List<Candle> candles, int period);
    decimal CalculateAdx(List<Candle> candles, int period);
    string GetCprBias(List<Candle> candles);
    void ApplyKeltner(List<Candle> candles, int emaPeriod, int atrPeriod, double multiplierInner, double multiplierOuter);
    void ApplyVwap(List<Candle> candles);
}

public class IndicatorService : IIndicatorService
{
    public decimal CalculateRsi(List<Candle> candles, int period)
    {
        if (candles.Count < period + 1) return 50;

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        decimal avgGain = gains.Take(period).Average();
        decimal avgLoss = losses.Take(period).Average();

        for (int i = period; i < gains.Count; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        if (avgLoss == 0) return 100;
        var rs = avgGain / avgLoss;
        return Math.Round(100 - (100 / (1 + rs)), 1);
    }

    public decimal CalculateAdx(List<Candle> candles, int period)
    {
        if (candles.Count < period * 2) return 20;

        var plusDm = new List<decimal>();
        var minusDm = new List<decimal>();
        var tr = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var upMove = candles[i].High - candles[i - 1].High;
            var downMove = candles[i - 1].Low - candles[i].Low;

            plusDm.Add(upMove > downMove && upMove > 0 ? upMove : 0);
            minusDm.Add(downMove > upMove && downMove > 0 ? downMove : 0);

            var highLow = candles[i].High - candles[i].Low;
            var highClose = Math.Abs(candles[i].High - candles[i - 1].Close);
            var lowClose = Math.Abs(candles[i].Low - candles[i - 1].Close);
            tr.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
        }

        decimal smoothPlusDm = plusDm.Take(period).Sum();
        decimal smoothMinusDm = minusDm.Take(period).Sum();
        decimal smoothTr = tr.Take(period).Sum();

        var dxValues = new List<decimal>();

        for (int i = period; i < tr.Count; i++)
        {
            smoothPlusDm = smoothPlusDm - smoothPlusDm / period + plusDm[i];
            smoothMinusDm = smoothMinusDm - smoothMinusDm / period + minusDm[i];
            smoothTr = smoothTr - smoothTr / period + tr[i];

            var plusDi = smoothTr > 0 ? 100 * smoothPlusDm / smoothTr : 0;
            var minusDi = smoothTr > 0 ? 100 * smoothMinusDm / smoothTr : 0;
            var diSum = plusDi + minusDi;
            var dx = diSum > 0 ? 100 * Math.Abs(plusDi - minusDi) / diSum : 0;
            dxValues.Add(dx);
        }

        return dxValues.Count > 0 ? Math.Round(dxValues.TakeLast(period).Average(), 0) : 20;
    }

    public string GetCprBias(List<Candle> candles)
    {
        if (candles.Count < 2) return "Neutral";

        var prev = candles[^2];
        var pivot = (prev.High + prev.Low + prev.Close) / 3;
        var current = candles[^1].Close;

        if (current > pivot * 1.001m) return "Bullish";
        if (current < pivot * 0.999m) return "Bearish";
        return "Neutral";
    }

    // Keltner Channels: middle = EMA(close, emaPeriod); bands = middle ± mult * ATR(atrPeriod).
    public void ApplyKeltner(List<Candle> candles, int emaPeriod, int atrPeriod, double multiplierInner, double multiplierOuter)
    {
        if (candles.Count == 0) return;

        var ema = Ema(candles.Select(c => c.Close).ToList(), emaPeriod);
        var atr = AtrSeries(candles, atrPeriod);
        var m1 = (decimal)multiplierInner;
        var m2 = (decimal)multiplierOuter;

        for (int i = 0; i < candles.Count; i++)
        {
            if (ema[i] is not { } mid || atr[i] <= 0m)
                continue;

            candles[i].KeltnerMid = mid;
            candles[i].KeltnerUpperInner = mid + m1 * atr[i];
            candles[i].KeltnerLowerInner = mid - m1 * atr[i];
            candles[i].KeltnerUpperOuter = mid + m2 * atr[i];
            candles[i].KeltnerLowerOuter = mid - m2 * atr[i];
        }
    }

    // Session-anchored VWAP. NIFTY index candles carry zero volume, so we fall
    // back to a running average of the typical price (a reasonable VWAP proxy).
    public void ApplyVwap(List<Candle> candles)
    {
        DateTime? day = null;
        decimal cumPv = 0, cumVol = 0, cumTypical = 0;
        int cumCount = 0;

        foreach (var c in candles)
        {
            if (day is null || c.Timestamp.Date != day)
            {
                day = c.Timestamp.Date;
                cumPv = cumVol = cumTypical = 0;
                cumCount = 0;
            }

            var typical = (c.High + c.Low + c.Close) / 3m;
            cumPv += typical * c.Volume;
            cumVol += c.Volume;
            cumTypical += typical;
            cumCount++;

            c.Vwap = cumVol > 0 ? cumPv / cumVol : cumTypical / cumCount;
        }
    }

    private static decimal?[] Ema(List<decimal> values, int period)
    {
        var n = values.Count;
        var ema = new decimal?[n];
        if (n < period) return ema;

        var k = 2m / (period + 1);
        decimal sum = 0;
        for (int i = 0; i < period; i++) sum += values[i];
        var prev = sum / period;
        ema[period - 1] = prev;

        for (int i = period; i < n; i++)
        {
            prev = values[i] * k + prev * (1 - k);
            ema[i] = prev;
        }

        return ema;
    }

    private static decimal[] AtrSeries(List<Candle> candles, int period)
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
        if (n < period) return atr;

        decimal sum = 0;
        for (int i = 0; i < period; i++) sum += tr[i];
        atr[period - 1] = sum / period;
        for (int i = period; i < n; i++)
            atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period;

        return atr;
    }
}
