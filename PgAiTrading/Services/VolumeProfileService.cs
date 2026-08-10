using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IVolumeProfileService
{
  VolumeProfileLevels BuildLevels(List<Candle> sessionCandles, List<Candle>? prevSessionCandles = null);
}

public class VolumeProfileService : IVolumeProfileService
{
  private const decimal ValueAreaPct = 0.70m;
  private const decimal TickSize = 0.05m;

  public VolumeProfileLevels BuildLevels(List<Candle> sessionCandles, List<Candle>? prevSessionCandles = null)
  {
    var levels = new VolumeProfileLevels();

    if (sessionCandles.Count > 0)
    {
      var today = BuildProfile(sessionCandles);
      if (today is not null)
      {
        levels.Poc = today.Poc;
        levels.Vah = today.Vah;
        levels.Val = today.Val;
        levels.HasData = true;
      }
    }

    if (prevSessionCandles is { Count: > 0 })
    {
      var prev = BuildProfile(prevSessionCandles);
      if (prev is not null)
      {
        levels.PrevDayPoc = prev.Poc;
        levels.PrevDayVah = prev.Vah;
        levels.PrevDayVal = prev.Val;
      }
    }

    return levels;
  }

  private static ProfileResult? BuildProfile(List<Candle> candles)
  {
    if (candles.Count == 0)
      return null;

    var low = candles.Min(c => c.Low);
    var high = candles.Max(c => c.High);
    if (high <= low)
      return null;

    var buckets = new Dictionary<decimal, decimal>();
    foreach (var candle in candles)
    {
      var range = candle.High - candle.Low;
      var vol = (decimal)candle.Volume;
      if (vol <= 0)
        vol = 1;

      if (range <= 0)
      {
        var price = RoundPrice(candle.Close);
        buckets[price] = buckets.GetValueOrDefault(price) + vol;
        continue;
      }

      var steps = Math.Max(1, (int)Math.Ceiling(range / TickSize));
      var stepVol = vol / steps;
      for (var i = 0; i < steps; i++)
      {
        var price = RoundPrice(candle.Low + (range * i / steps));
        buckets[price] = buckets.GetValueOrDefault(price) + stepVol;
      }
    }

    if (buckets.Count == 0)
      return null;

    var poc = buckets.OrderByDescending(kv => kv.Value).First().Key;
    var totalVol = buckets.Values.Sum();
    var targetVol = totalVol * ValueAreaPct;

    var sorted = buckets.OrderBy(kv => kv.Key).ToList();
    var pocIndex = sorted.FindIndex(kv => kv.Key == poc);
    if (pocIndex < 0)
      pocIndex = sorted.Count / 2;

    decimal accumulated = sorted[pocIndex].Value;
    int lowIdx = pocIndex, highIdx = pocIndex;

    while (accumulated < targetVol && (lowIdx > 0 || highIdx < sorted.Count - 1))
    {
      var takeLow = lowIdx > 0;
      var takeHigh = highIdx < sorted.Count - 1;

      if (takeLow && takeHigh)
      {
        var lowVol = sorted[lowIdx - 1].Value;
        var highVol = sorted[highIdx + 1].Value;
        if (lowVol >= highVol)
        {
          lowIdx--;
          accumulated += sorted[lowIdx].Value;
        }
        else
        {
          highIdx++;
          accumulated += sorted[highIdx].Value;
        }
      }
      else if (takeLow)
      {
        lowIdx--;
        accumulated += sorted[lowIdx].Value;
      }
      else
      {
        highIdx++;
        accumulated += sorted[highIdx].Value;
      }
    }

    return new ProfileResult
    {
      Poc = poc,
      Val = sorted[lowIdx].Key,
      Vah = sorted[highIdx].Key
    };
  }

  private static decimal RoundPrice(decimal price) =>
    Math.Round(price / TickSize) * TickSize;

  private sealed class ProfileResult
  {
    public decimal Poc { get; set; }
    public decimal Vah { get; set; }
    public decimal Val { get; set; }
  }
}
