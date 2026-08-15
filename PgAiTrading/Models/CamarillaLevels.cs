namespace PgAiTrading.Models;

/// <summary>
/// Camarilla pivot levels from previous session H/L/C. H1/L1 omitted (mild S/R).
/// </summary>
public class CamarillaLevels
{
  public decimal H4 { get; set; }
  public decimal H3 { get; set; }
  public decimal H2 { get; set; }
  public decimal Pivot { get; set; }
  public decimal L2 { get; set; }
  public decimal L3 { get; set; }
  public decimal L4 { get; set; }
  public decimal PrevHigh { get; set; }
  public decimal PrevLow { get; set; }
  public decimal PrevClose { get; set; }
  public bool HasData { get; set; }

  public TrendDirection GetBias(decimal price)
  {
    if (!HasData || price <= 0)
      return TrendDirection.Neutral;

    if (price > Pivot && price > H2)
      return TrendDirection.Buy;

    if (price < Pivot && price < L2)
      return TrendDirection.Sell;

    return TrendDirection.Neutral;
  }

  public TrendDirection GetBandBias(decimal price)
  {
    if (!HasData || price <= 0)
      return TrendDirection.Neutral;

    if (price > H3)
      return TrendDirection.Buy;

    if (price < L3)
      return TrendDirection.Sell;

    return TrendDirection.Neutral;
  }
}

public static class CamarillaCalculator
{
  /// <summary>
  /// Standard Camarilla from previous session OHLC.
  /// H4/L4 = C ± (H−L)×1.1/2; H3/L3 ÷4; H2/L2 ÷6; PP = (H+L+C)/3.
  /// </summary>
  public static CamarillaLevels FromPreviousDay(Candle prevDay)
  {
    var range = prevDay.High - prevDay.Low;
    var close = prevDay.Close;

    if (range <= 0 || close <= 0 || prevDay.High < prevDay.Low)
      return new CamarillaLevels();

    var scaled = range * 1.1m;

    return new CamarillaLevels
    {
      HasData = true,
      H4 = Round(close + scaled / 2m),
      H3 = Round(close + scaled / 4m),
      H2 = Round(close + scaled / 6m),
      Pivot = Round((prevDay.High + prevDay.Low + close) / 3m),
      L2 = Round(close - scaled / 6m),
      L3 = Round(close - scaled / 4m),
      L4 = Round(close - scaled / 2m),
      PrevHigh = prevDay.High,
      PrevLow = prevDay.Low,
      PrevClose = close
    };
  }

  /// <summary>
  /// Build Camarilla from the best available previous session bar.
  /// Prefers completed daily candles near <paramref name="referencePrice"/>;
  /// falls back to aggregating the prior intraday session — never a single 1H bar.
  /// </summary>
  public static CamarillaLevels FromAvailableSessions(
      IReadOnlyList<Candle> dailyCandles,
      IReadOnlyList<Candle>? previousIntradaySession,
      decimal referencePrice,
      DateTime? asOfDate = null)
  {
    var prevDay = ResolvePreviousSessionBar(dailyCandles, previousIntradaySession, referencePrice, asOfDate);
    return prevDay is null ? new CamarillaLevels() : FromPreviousDay(prevDay);
  }

  public static Candle? ResolvePreviousSessionBar(
      IReadOnlyList<Candle> dailyCandles,
      IReadOnlyList<Candle>? previousIntradaySession,
      decimal referencePrice,
      DateTime? asOfDate = null)
  {
    var today = (asOfDate ?? DateTime.Today).Date;

    Candle? fromDaily = null;
    if (dailyCandles.Count > 0)
    {
      fromDaily = dailyCandles
          .Where(c => c.Timestamp.Date < today)
          .OrderBy(c => c.Timestamp)
          .LastOrDefault();

      // If feed has no "today" bar yet, last bar may still be yesterday.
      if (fromDaily is null && dailyCandles.Count >= 1)
      {
        var last = dailyCandles[^1];
        if (last.Timestamp.Date < today)
          fromDaily = last;
        else if (dailyCandles.Count >= 2)
          fromDaily = dailyCandles[^2];
      }
    }

    if (fromDaily is not null && IsPlausibleSessionBar(fromDaily, referencePrice))
      return fromDaily;

    if (previousIntradaySession is { Count: > 0 })
    {
      var aggregated = AggregateSession(previousIntradaySession);
      if (IsPlausibleSessionBar(aggregated, referencePrice))
        return aggregated;
    }

    // Prefer aggregated intraday even when slightly off, before a mismatched daily demo bar.
    if (previousIntradaySession is { Count: > 0 })
    {
      var aggregated = AggregateSession(previousIntradaySession);
      if (aggregated.High > aggregated.Low && aggregated.Close > 0)
        return aggregated;
    }

    return fromDaily is not null && fromDaily.High > fromDaily.Low && fromDaily.Close > 0
        ? fromDaily
        : null;
  }

  public static Candle AggregateSession(IReadOnlyList<Candle> session)
  {
    var ordered = session.OrderBy(c => c.Timestamp).ToList();
    return new Candle
    {
      Timestamp = ordered[0].Timestamp.Date,
      Open = ordered[0].Open,
      High = ordered.Max(c => c.High),
      Low = ordered.Min(c => c.Low),
      Close = ordered[^1].Close,
      Volume = ordered.Sum(c => c.Volume)
    };
  }

  /// <summary>
  /// Rejects demo/mismatched bars (e.g. fallback price 1000 while live chart is 1300).
  /// </summary>
  public static bool IsPlausibleSessionBar(Candle bar, decimal referencePrice)
  {
    if (bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0 || bar.High < bar.Low)
      return false;

    if (referencePrice <= 0)
      return true;

    var ratio = bar.Close / referencePrice;
    return ratio >= 0.88m && ratio <= 1.12m;
  }

  private static decimal Round(decimal value) => Math.Round(value, 2);
}
