namespace PgAiTrading.Models;

/// <summary>
/// Camarilla pivot levels from the previous pivot-period H/L/C. H1/L1 omitted (mild S/R).
/// Pivot period follows chart timeframe (TradingView Auto-style).
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
  /// <summary>Pivot period used: 1D, 1W, or 1M.</summary>
  public string PivotTimeframe { get; set; } = "1D";

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
  /// TradingView-style Auto mapping: intraday → daily, daily → weekly, weekly → monthly.
  /// </summary>
  public static string ResolvePivotTimeframe(string? chartTimeframe) =>
      (chartTimeframe ?? "5m").Trim().ToUpperInvariant() switch
      {
          "1W" => "1M",
          "1D" => "1W",
          _ => "1D"
      };

  /// <summary>
  /// Standard Camarilla from previous period OHLC.
  /// H4/L4 = C ± (H−L)×1.1/2; H3/L3 ÷4; H2/L2 ÷6; PP = (H+L+C)/3.
  /// </summary>
  public static CamarillaLevels FromPreviousDay(Candle prevDay, string pivotTimeframe = "1D")
  {
    var range = prevDay.High - prevDay.Low;
    var close = prevDay.Close;

    if (range <= 0 || close <= 0 || prevDay.High < prevDay.Low)
      return new CamarillaLevels { PivotTimeframe = pivotTimeframe };

    var scaled = range * 1.1m;

    return new CamarillaLevels
    {
      HasData = true,
      PivotTimeframe = pivotTimeframe,
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
  /// Build Camarilla for the chart timeframe (Auto pivot period).
  /// </summary>
  public static CamarillaLevels ForChartTimeframe(
      string? chartTimeframe,
      IReadOnlyList<Candle> chartCandles,
      IReadOnlyList<Candle> dailyCandles,
      IReadOnlyList<Candle>? previousIntradaySession,
      decimal referencePrice,
      DateTime? asOfDate = null)
  {
    var pivotTf = ResolvePivotTimeframe(chartTimeframe);

    if (pivotTf == "1D")
    {
      var levels = FromAvailableSessions(dailyCandles, previousIntradaySession, referencePrice, asOfDate);
      levels.PivotTimeframe = "1D";
      return levels;
    }

    if (pivotTf == "1W")
    {
      // On a weekly chart the candles are already weeks; on daily, aggregate.
      var weekly = string.Equals(chartTimeframe, "1W", StringComparison.OrdinalIgnoreCase)
          && chartCandles.Count >= 2
          ? chartCandles.ToList()
          : ToWeeklyBars(dailyCandles.Count > 0 ? dailyCandles : chartCandles);
      return FromPreviousCompletedBar(weekly, referencePrice, "1W", asOfDate);
    }

    // 1M — aggregate daily (or chart candles) into months
    var source = dailyCandles.Count > 0 ? dailyCandles : chartCandles;
    var monthly = ToMonthlyBars(source);
    return FromPreviousCompletedBar(monthly, referencePrice, "1M", asOfDate);
  }

  /// <summary>
  /// TradingView-style historical Camarilla: stepped levels per pivot period (default ~15 lookback).
  /// Each segment uses the previous completed period H/L/C and spans the following period.
  /// </summary>
  public static IReadOnlyList<CamarillaSegment> BuildHistory(
      string? chartTimeframe,
      IReadOnlyList<Candle> chartCandles,
      IReadOnlyList<Candle> dailyCandles,
      decimal referencePrice,
      DateTime? asOfDate = null,
      int lookbackPeriods = 15)
  {
    var pivotTf = ResolvePivotTimeframe(chartTimeframe);
    var asOf = (asOfDate ?? DateTime.Today).Date;
    var periodBars = GetPeriodBars(chartTimeframe, chartCandles, dailyCandles, pivotTf);
    if (periodBars.Count < 2)
      return Array.Empty<CamarillaSegment>();

    var usable = periodBars
        .Where(b => b.High > b.Low && b.Close > 0)
        .Where(b => referencePrice <= 0 || IsLooseHistoryBar(b, referencePrice))
        .OrderBy(b => b.Timestamp)
        .ToList();

    if (usable.Count < 2)
      return Array.Empty<CamarillaSegment>();

    var segments = new List<CamarillaSegment>();
    var startIndex = Math.Max(0, usable.Count - lookbackPeriods - 1);

    for (var i = startIndex; i < usable.Count - 1; i++)
    {
      var source = usable[i];
      var active = usable[i + 1];
      var levels = FromPreviousDay(source, pivotTf);
      if (!levels.HasData)
        continue;

      var start = PeriodStart(active.Timestamp, pivotTf);
      var end = i + 2 < usable.Count
          ? PeriodStart(usable[i + 2].Timestamp, pivotTf)
          : PeriodEnd(start, pivotTf);

      // Current (possibly incomplete) period: extend far enough to cover live candles.
      if (i == usable.Count - 2 && !IsCompletedPeriodBar(active, pivotTf, asOf))
        end = PeriodEnd(asOf, pivotTf);

      if (end <= start)
        end = PeriodEnd(start, pivotTf);

      segments.Add(new CamarillaSegment(
          start,
          end,
          levels.H4,
          levels.H3,
          levels.Pivot,
          levels.L3,
          levels.L4));
    }

    return segments;
  }

  private static List<Candle> GetPeriodBars(
      string? chartTimeframe,
      IReadOnlyList<Candle> chartCandles,
      IReadOnlyList<Candle> dailyCandles,
      string pivotTf)
  {
    if (pivotTf == "1D")
      return (dailyCandles.Count > 0 ? dailyCandles : chartCandles)
          .OrderBy(c => c.Timestamp)
          .ToList();

    if (pivotTf == "1W")
    {
      if (string.Equals(chartTimeframe, "1W", StringComparison.OrdinalIgnoreCase)
          && chartCandles.Count >= 2)
        return chartCandles.OrderBy(c => c.Timestamp).ToList();

      return ToWeeklyBars(dailyCandles.Count > 0 ? dailyCandles : chartCandles);
    }

    return ToMonthlyBars(dailyCandles.Count > 0 ? dailyCandles : chartCandles);
  }

  /// <summary>Wider than session plausibility so historical steps survive mild price drift.</summary>
  private static bool IsLooseHistoryBar(Candle bar, decimal referencePrice)
  {
    var ratio = bar.Close / referencePrice;
    return ratio >= 0.55m && ratio <= 1.85m;
  }

  private static DateTime PeriodStart(DateTime timestamp, string pivotTf) =>
      pivotTf switch
      {
          "1W" => GetWeekStart(timestamp),
          "1M" => new DateTime(timestamp.Year, timestamp.Month, 1),
          _ => timestamp.Date
      };

  private static DateTime PeriodEnd(DateTime timestamp, string pivotTf) =>
      pivotTf switch
      {
          "1W" => PeriodStart(timestamp, pivotTf).AddDays(7),
          "1M" => PeriodStart(timestamp, pivotTf).AddMonths(1),
          _ => timestamp.Date.AddDays(1)
      };

  /// <summary>
  /// Build Camarilla from the best available previous session bar (daily pivot period).
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
    return prevDay is null ? new CamarillaLevels() : FromPreviousDay(prevDay, "1D");
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

  public static CamarillaLevels FromPreviousCompletedBar(
      IReadOnlyList<Candle> periodBars,
      decimal referencePrice,
      string pivotTimeframe,
      DateTime? asOfDate = null)
  {
    if (periodBars.Count < 2)
      return new CamarillaLevels { PivotTimeframe = pivotTimeframe };

    var asOf = asOfDate ?? DateTime.Today;
    Candle? prev = null;
    for (var i = periodBars.Count - 1; i >= 0; i--)
    {
      var bar = periodBars[i];
      if (IsCompletedPeriodBar(bar, pivotTimeframe, asOf))
      {
        prev = bar;
        break;
      }
    }

    prev ??= periodBars.Count >= 2 ? periodBars[^2] : periodBars[^1];

    if (!IsPlausibleSessionBar(prev, referencePrice) && periodBars.Count >= 3)
    {
      var earlier = periodBars[^3];
      if (IsPlausibleSessionBar(earlier, referencePrice))
        prev = earlier;
    }

    return prev.Close > 0 && prev.High >= prev.Low
        ? FromPreviousDay(prev, pivotTimeframe)
        : new CamarillaLevels { PivotTimeframe = pivotTimeframe };
  }

  public static bool IsCompletedPeriodBar(Candle bar, string pivotTimeframe, DateTime asOf)
  {
    var asOfDate = asOf.Date;
    return pivotTimeframe switch
    {
        "1W" => GetWeekStart(bar.Timestamp) < GetWeekStart(asOfDate),
        "1M" => new DateTime(bar.Timestamp.Year, bar.Timestamp.Month, 1)
            < new DateTime(asOfDate.Year, asOfDate.Month, 1),
        _ => bar.Timestamp.Date < asOfDate
    };
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

  private static DateTime GetWeekStart(DateTime date)
  {
    var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
    return date.Date.AddDays(-diff);
  }

  private static List<Candle> ToWeeklyBars(IReadOnlyList<Candle> daily) =>
      daily
          .GroupBy(c => GetWeekStart(c.Timestamp))
          .OrderBy(g => g.Key)
          .Select(g =>
          {
            var ordered = g.OrderBy(c => c.Timestamp).ToList();
            return new Candle
            {
              Timestamp = g.Key,
              Open = ordered[0].Open,
              High = ordered.Max(c => c.High),
              Low = ordered.Min(c => c.Low),
              Close = ordered[^1].Close,
              Volume = ordered.Sum(c => c.Volume)
            };
          })
          .ToList();

  private static List<Candle> ToMonthlyBars(IReadOnlyList<Candle> daily) =>
      daily
          .GroupBy(c => new DateTime(c.Timestamp.Year, c.Timestamp.Month, 1))
          .OrderBy(g => g.Key)
          .Select(g =>
          {
            var ordered = g.OrderBy(c => c.Timestamp).ToList();
            return new Candle
            {
              Timestamp = g.Key,
              Open = ordered[0].Open,
              High = ordered.Max(c => c.High),
              Low = ordered.Min(c => c.Low),
              Close = ordered[^1].Close,
              Volume = ordered.Sum(c => c.Volume)
            };
          })
          .ToList();

  private static decimal Round(decimal value) => Math.Round(value, 2);
}
