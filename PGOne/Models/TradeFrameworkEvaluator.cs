namespace PGOne.Models;

/// <summary>
/// Intraday trade framework with TPO / volume-profile confirmation.
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

  public static bool IsRotationRegime(
    decimal adx1H,
    decimal price,
    VolumeProfileLevels profile,
    StrategyConfig config) =>
    adx1H < config.AdxWeakThreshold
    && profile.HasData
    && profile.IsInsideValueArea(price);

  public static TrendDirection GetTradeDirection(
    TrendDirection marketBias,
    TrendDirection trend15M,
    decimal adx1H,
    decimal rsi1H,
    decimal price,
    VolumeProfileLevels profile,
    TpoConfirmationAnalysis tpo,
    StrategyConfig config)
  {
    if (marketBias == TrendDirection.Neutral)
      return TrendDirection.Neutral;

    if (trend15M != marketBias)
      return TrendDirection.Neutral;

    if (adx1H < config.AdxWeakThreshold)
      return TrendDirection.Neutral;

    if (adx1H < config.MinimumAdx)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Buy && rsi1H < config.RsiBullThreshold)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Sell && rsi1H > config.RsiBearThreshold)
      return TrendDirection.Neutral;

    if (!tpo.Confirms(marketBias))
      return TrendDirection.Neutral;

    if (tpo.TrendDayOutsideVa)
    {
      if (marketBias == TrendDirection.Buy && !profile.IsAboveValueArea(price))
        return TrendDirection.Neutral;

      if (marketBias == TrendDirection.Sell && !profile.IsBelowValueArea(price))
        return TrendDirection.Neutral;
    }

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
    bool waitForReversal,
    bool isRotationRegime) =>
    !waitForReversal
    && !isRotationRegime
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
    TpoConfirmationAnalysis tpo,
    bool isRotationRegime,
    bool frameworkReady)
  {
    if (isRotationRegime)
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

    if (tpo.Confirms(tradeDirection)) score += 10;
    if (tpo.StrongTrendDay) score += 5;

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
    decimal adx1H,
    decimal rsi1H,
    bool aboveVwap,
    FootprintAnalysis footprint,
    TpoConfirmationAnalysis tpo,
    bool waitForReversal,
    bool isRotationRegime,
    StrategyConfig config)
  {
    if (waitForReversal)
      return "Wait — 5m RSI oversold";

    if (isRotationRegime)
      return "Rotation inside VA — avoid breakouts";

    if (trend1H == TrendDirection.Neutral)
      return "Wait — 1H SuperTrend neutral";

    if (marketBias == TrendDirection.Neutral)
      return aboveVwap ? "Wait — 1H bearish vs VWAP" : "Wait — 1H bullish vs VWAP";

    if (trend15M != marketBias)
      return "Wait — 15m SuperTrend not aligned";

    if (adx1H < config.AdxWeakThreshold)
      return $"Wait — ADX {adx1H:0} choppy (<{config.AdxWeakThreshold:0})";

    if (adx1H < config.MinimumAdx)
      return $"Wait — ADX {adx1H:0} moderate, need ≥{config.MinimumAdx:0}";

    if (marketBias == TrendDirection.Buy && rsi1H < config.RsiBullThreshold)
      return $"Wait — 1H RSI(28) {rsi1H:0} < {config.RsiBullThreshold:0}";

    if (marketBias == TrendDirection.Sell && rsi1H > config.RsiBearThreshold)
      return $"Wait — 1H RSI(28) {rsi1H:0} > {config.RsiBearThreshold:0}";

    if (!tpo.Confirms(marketBias))
      return $"Wait — POC: {tpo.Summary}";

    if (tpo.TrendDayOutsideVa)
    {
      if (marketBias == TrendDirection.Buy && !tpo.AboveValueArea)
        return "Wait — trend day needs price above VAH";

      if (marketBias == TrendDirection.Sell && !tpo.BelowValueArea)
        return "Wait — trend day needs price below VAL";
    }

    if (trend5MEntry != marketBias)
      return "Wait — 5m entry SuperTrend (7,2.5) not triggered";

    if (!footprint.Confirms(marketBias))
      return "Wait — footprint not confirmed";

    return tpo.StrongTrendDay ? "Ready — strong trend day" : "Ready";
  }
}
