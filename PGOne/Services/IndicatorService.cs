using PGOne.Models;

namespace PGOne.Services;

public interface IIndicatorService
{
    decimal CalculateRsi(List<Candle> candles, int period);
    decimal CalculateAdx(List<Candle> candles, int period);
    string GetCprBias(List<Candle> candles);
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
}
