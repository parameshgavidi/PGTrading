namespace PGOne.Models;

/// <summary>
/// Five-step intraday trade framework:
/// 1. Market bias — 1H ST + VWAP
/// 2. Trade direction — 15M ST + ADX + RSI
/// 3. Entry — 5M ST (7,2.5) trigger
/// 4. Footprint confirmation — Delta + imbalances + no opposing absorption
/// 5. Exit — Prev POC / VAH / VAL / 5M ST reversal
/// </summary>
public static class TradeFrameworkEvaluator
{
  public static TrendDirection GetMarketBias(TrendDirection trend1H, bool aboveVwap) =>
    trend1H switch
    {
      TrendDirection.Buy when aboveVwap => TrendDirection.Buy,
      TrendDirection.Sell when !aboveVwap => TrendDirection.Sell,
      _ => TrendDirection.Neutral
    };

  public static TrendDirection GetTradeDirection(
    TrendDirection marketBias,
    TrendDirection trend15M,
    decimal adx,
    decimal rsi,
    StrategyConfig config)
  {
    if (marketBias == TrendDirection.Neutral)
      return TrendDirection.Neutral;

    if (trend15M != marketBias)
      return TrendDirection.Neutral;

    if (adx < config.MinimumAdx)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Buy && rsi < config.RsiBullThreshold)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Sell && rsi > config.RsiBearThreshold)
      return TrendDirection.Neutral;

    return marketBias;
  }

  public static bool EntryTriggered(TrendDirection tradeDirection, TrendDirection trend5MEntry) =>
    tradeDirection != TrendDirection.Neutral && trend5MEntry == tradeDirection;

  public static bool FootprintConfirmed(TrendDirection tradeDirection, FootprintAnalysis footprint) =>
    tradeDirection != TrendDirection.Neutral && footprint.Confirms(tradeDirection);

  public static bool IsFrameworkReady(
    TrendDirection tradeDirection,
    TrendDirection trend5MEntry,
    FootprintAnalysis footprint,
    bool waitForReversal) =>
    !waitForReversal
    && tradeDirection != TrendDirection.Neutral
    && EntryTriggered(tradeDirection, trend5MEntry)
    && FootprintConfirmed(tradeDirection, footprint);

  public static int CalculateScore(
    TrendDirection marketBias,
    TrendDirection tradeDirection,
    TrendDirection trend1H,
    TrendDirection trend15M,
    TrendDirection trend5MEntry,
    TrendStrength strength1H,
    bool aboveVwap,
    FootprintAnalysis footprint,
    bool isRangebound,
    bool frameworkReady)
  {
    if (isRangebound)
      return 45;

    var score = 25;

    if (marketBias != TrendDirection.Neutral) score += 10;
    if (tradeDirection != TrendDirection.Neutral) score += 15;
    if (trend1H == tradeDirection) score += 5;
    if (trend15M == tradeDirection) score += 10;
    if (trend5MEntry == tradeDirection) score += 10;

    score += strength1H switch
    {
      TrendStrength.Strong => 15,
      TrendStrength.Moderate => 8,
      _ => 0
    };

    if (tradeDirection == TrendDirection.Buy && aboveVwap) score += 5;
    if (tradeDirection == TrendDirection.Sell && !aboveVwap) score += 5;

    if (footprint.Confirms(tradeDirection)) score += 15;
    else if (footprint.PositiveDelta || footprint.NegativeDelta) score += 5;

    if (frameworkReady) score += 10;

    return Math.Clamp(score, 0, 99);
  }

  public static string GetBlockingReason(
    TrendDirection marketBias,
    TrendDirection tradeDirection,
    TrendDirection trend1H,
    TrendDirection trend15M,
    TrendDirection trend5MEntry,
    decimal adx,
    decimal rsi,
    bool aboveVwap,
    FootprintAnalysis footprint,
    bool waitForReversal,
    bool isRangebound,
    StrategyConfig config)
  {
    if (waitForReversal)
      return "Wait — 5m RSI oversold";

    if (isRangebound)
      return "Range-bound — Keltner fade";

    if (trend1H == TrendDirection.Neutral)
      return "Wait — 1H SuperTrend neutral";

    if (marketBias == TrendDirection.Neutral)
      return aboveVwap ? "Wait — 1H bearish vs VWAP" : "Wait — 1H bullish vs VWAP";

    if (trend15M != marketBias)
      return "Wait — 15m SuperTrend not aligned";

    if (adx < config.MinimumAdx)
      return $"Wait — ADX {adx:0} < {config.MinimumAdx:0}";

    if (marketBias == TrendDirection.Buy && rsi < config.RsiBullThreshold)
      return $"Wait — RSI {rsi:0} < {config.RsiBullThreshold:0}";

    if (marketBias == TrendDirection.Sell && rsi > config.RsiBearThreshold)
      return $"Wait — RSI {rsi:0} > {config.RsiBearThreshold:0}";

    if (trend5MEntry != marketBias)
      return "Wait — 5m entry SuperTrend (7,2.5) not triggered";

    if (!footprint.Confirms(marketBias))
      return "Wait — footprint not confirmed";

    return "Ready";
  }
}
