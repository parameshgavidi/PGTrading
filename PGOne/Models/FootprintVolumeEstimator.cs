namespace PGOne.Models;

/// <summary>
/// Splits candle activity into buy/sell volume proxies for footprint delta.
/// When exchange volume is zero (indices), range is used as the activity proxy.
/// </summary>
public static class FootprintVolumeEstimator
{
  public static (decimal Buy, decimal Sell) EstimateBidAskVolume(Candle candle)
  {
    var range = candle.High - candle.Low;
    var vol = (decimal)candle.Volume;

    if (vol <= 0)
    {
      vol = range > 0 ? range : Math.Abs(candle.Close - candle.Open);
      if (vol <= 0)
        return (0, 0);
    }

    if (range <= 0)
    {
      var body = candle.Close - candle.Open;
      if (body > 0)
        return (vol, 0);

      if (body < 0)
        return (0, vol);

      return (vol / 2m, vol / 2m);
    }

    var buyRatio = (candle.Close - candle.Low) / range;
    var sellRatio = (candle.High - candle.Close) / range;
    return (vol * buyRatio, vol * sellRatio);
  }
}
