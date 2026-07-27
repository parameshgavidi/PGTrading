using PGOne.Models;

namespace PGOne.Services;

public interface IFootprintService
{
  FootprintAnalysis Analyze(List<Candle> candles5M, TrendDirection bias);
}

public class FootprintService : IFootprintService
{
  private const int Lookback = 8;
  private const int MinStackedBars = 3;
  private const decimal ImbalanceRatio = 1.4m;
  private const decimal AbsorptionVolumeRatio = 1.5m;

  public FootprintAnalysis Analyze(List<Candle> candles5M, TrendDirection bias)
  {
    if (candles5M.Count < Lookback + 2)
      return new FootprintAnalysis { Summary = "Insufficient 5m data" };

    var slice = candles5M.TakeLast(Lookback).ToList();
    var avgVolume = slice.Average(c => (decimal)c.Volume);
    if (avgVolume <= 0)
      avgVolume = 1;

    decimal buyVol = 0, sellVol = 0;
    var buyDominantStreak = 0;
    var sellDominantStreak = 0;
    var maxBuyStreak = 0;
    var maxSellStreak = 0;

    foreach (var candle in slice)
    {
      var (buy, sell) = EstimateBidAskVolume(candle);
      buyVol += buy;
      sellVol += sell;

      if (buy > sell * ImbalanceRatio)
      {
        buyDominantStreak++;
        maxBuyStreak = Math.Max(maxBuyStreak, buyDominantStreak);
        sellDominantStreak = 0;
      }
      else if (sell > buy * ImbalanceRatio)
      {
        sellDominantStreak++;
        maxSellStreak = Math.Max(maxSellStreak, sellDominantStreak);
        buyDominantStreak = 0;
      }
      else
      {
        buyDominantStreak = 0;
        sellDominantStreak = 0;
      }
    }

    var delta = buyVol - sellVol;
    var last = slice[^1];
    var recentHigh = slice.Max(c => c.High);
    var recentLow = slice.Min(c => c.Low);
    var range = last.High - last.Low;
    var avgRange = slice.Average(c => c.High - c.Low);
    var volRatio = last.Volume / avgVolume;

    var absorptionAgainstLong = volRatio >= AbsorptionVolumeRatio
      && last.Close >= recentHigh * 0.997m
      && last.Close < last.Open
      && range > 0
      && (last.Close - last.Low) < range * 0.35m;

    var absorptionAgainstShort = volRatio >= AbsorptionVolumeRatio
      && last.Close <= recentLow * 1.003m
      && last.Close > last.Open
      && range > 0
      && (last.High - last.Close) < range * 0.35m;

    var unfinishedAuction = range > 0
      && avgRange > 0
      && (last.High - last.Close) < range * 0.05m
      && last.Close > last.Open;

    var nearVolumeNode = avgRange > 0 && range < avgRange * 0.6m && volRatio >= 1.2m;

    var result = new FootprintAnalysis
    {
      Delta = Math.Round(delta, 2),
      PositiveDelta = delta > 0,
      NegativeDelta = delta < 0,
      UsesVolumeProxy = slice.All(c => c.Volume <= 0),
      StackedBuyImbalance = maxBuyStreak >= MinStackedBars,
      StackedSellImbalance = maxSellStreak >= MinStackedBars,
      AbsorptionAgainstLong = absorptionAgainstLong,
      AbsorptionAgainstShort = absorptionAgainstShort,
      NearVolumeNode = nearVolumeNode,
      HasUnfinishedAuction = unfinishedAuction
    };

    result.Summary = BuildSummary(result, bias);
    return result;
  }

  private static (decimal Buy, decimal Sell) EstimateBidAskVolume(Candle candle) =>
    FootprintVolumeEstimator.EstimateBidAskVolume(candle);

  private static string BuildSummary(FootprintAnalysis fp, TrendDirection bias)
  {
    if (bias == TrendDirection.Buy && fp.ConfirmsLong)
      return "Delta +, stacked buy imbalances, no absorption against";

    if (bias == TrendDirection.Sell && fp.ConfirmsShort)
      return "Delta −, stacked sell imbalances, no absorption against";

    var parts = new List<string>();
    if (fp.PositiveDelta) parts.Add("Delta +");
    else if (fp.NegativeDelta) parts.Add("Delta −");
    else parts.Add("Delta flat");

    if (fp.UsesVolumeProxy)
      parts.Add("range proxy (no volume)");

    if (fp.StackedBuyImbalance) parts.Add("buy imbalances");
    if (fp.StackedSellImbalance) parts.Add("sell imbalances");
    if (fp.AbsorptionAgainstLong) parts.Add("absorption vs long");
    if (fp.AbsorptionAgainstShort) parts.Add("absorption vs short");

    return parts.Count > 0 ? string.Join(", ", parts) : "Neutral footprint";
  }
}
