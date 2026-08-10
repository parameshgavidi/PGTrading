namespace PGOneTrade.Models;

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

  public static bool IsRangebound(decimal rsi1H, StrategyConfig config) =>
    rsi1H >= config.RsiBearThreshold && rsi1H <= config.RsiBullThreshold;

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

    if (IsRangebound(rsi1H, config))
      return TrendDirection.Neutral;

    if (trend15M != marketBias)
      return TrendDirection.Neutral;

    if (adx1H < config.AdxWeakThreshold)
      return TrendDirection.Neutral;

    if (adx1H < config.MinimumAdx)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Buy && !RsiConfirmsLong(rsi1H, config))
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Sell && !RsiConfirmsShort(rsi1H, config))
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
    bool isRotationRegime,
    bool isRangebound) =>
    !waitForReversal
    && !isRotationRegime
    && !isRangebound
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
    bool isRangebound,
    bool frameworkReady)
  {
    var score = 25;

    if (marketBias != TrendDirection.Neutral) score += 10;
    if (tradeDirection != TrendDirection.Neutral) score += 15;

    var alignment = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (alignment != TrendDirection.Neutral)
    {
      if (trend1H == alignment) score += 5;
      if (trend15M == alignment) score += 10;
      if (trend5MEntry == alignment) score += 10;

      if (alignment == TrendDirection.Buy && aboveVwap) score += 5;
      if (alignment == TrendDirection.Sell && !aboveVwap) score += 5;
    }

    score += strength1H switch
    {
      TrendStrength.Strong => 15,
      TrendStrength.Moderate => 8,
      _ => 0
    };

    if (tradeDirection != TrendDirection.Neutral && tpo.Confirms(tradeDirection)) score += 10;
    else if (marketBias != TrendDirection.Neutral && tpo.Confirms(marketBias)) score += 5;

    if (tpo.StrongTrendDay) score += 5;

    if (footprint.Confirms(tradeDirection)) score += 15;
    else if (tradeDirection != TrendDirection.Neutral && (footprint.PositiveDelta || footprint.NegativeDelta)) score += 5;
    else if (marketBias != TrendDirection.Neutral)
    {
      var flowBias = footprint.PositiveDelta
        ? TrendDirection.Buy
        : footprint.NegativeDelta
          ? TrendDirection.Sell
          : TrendDirection.Neutral;
      if (flowBias == marketBias) score += 5;
    }

    if (frameworkReady) score += 10;

    var primaryBias = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (primaryBias != TrendDirection.Neutral
        && FootprintDisplayHelper.FootprintOpposesBias(footprint, primaryBias))
      score -= 22;

    if (tradeDirection != TrendDirection.Neutral && !footprint.Confirms(tradeDirection))
      score = Math.Min(score, 82);

    if (tradeDirection != TrendDirection.Neutral && !EntryTriggered(tradeDirection, trend5MEntry))
      score = Math.Min(score, 78);

    if (primaryBias != TrendDirection.Neutral
        && FootprintDisplayHelper.FootprintOpposesBias(footprint, primaryBias))
      score = Math.Min(score, 68);

    if (!frameworkReady)
      score = Math.Min(score, 85);

    if (frameworkReady)
      score = Math.Max(score, 75);

    if (isRotationRegime)
      score = Math.Min(score, 48);
    else if (isRangebound)
      score = Math.Min(score, 54);

    return Math.Clamp(score, 0, 99);
  }

  public static string GetScoreStrengthLabel(
    int score,
    bool isRangebound,
    bool isRotationRegime,
    bool frameworkReady,
    bool footprintConflict = false)
  {
    if (frameworkReady)
      return "Ready";

    if (footprintConflict)
      return "Flow Conflict";

    if (isRotationRegime)
      return "Rotation";

    if (isRangebound)
      return "Range-bound";

    return score switch
    {
      >= 75 => "Strong Setup",
      >= 55 => "Moderate Setup",
      _ => "Weak Setup"
    };
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
    bool isRangebound,
    StrategyConfig config)
  {
    if (waitForReversal)
      return "Wait — 5m RSI oversold";

    if (isRotationRegime)
      return "Rotation inside VA — avoid breakouts";

    if (isRangebound)
      return "Range-bound — 1H RSI(28) between 45–55";

    if (trend1H == TrendDirection.Neutral)
      return "Wait — 1H SuperTrend neutral";

    if (marketBias == TrendDirection.Neutral)
    {
      if (trend1H == TrendDirection.Buy && !aboveVwap)
        return "Step 1 — 1H ST bullish but price below session VWAP";

      if (trend1H == TrendDirection.Sell && aboveVwap)
        return "Step 1 — 1H ST bearish but price above session VWAP";

      return "Step 1 — 1H SuperTrend and VWAP not aligned";
    }

    if (trend15M != marketBias)
      return "Wait — 15m SuperTrend not aligned";

    if (adx1H < config.AdxWeakThreshold)
      return $"Wait — ADX {adx1H:0} choppy (<{config.AdxWeakThreshold:0})";

    if (adx1H < config.MinimumAdx)
      return $"Wait — ADX {adx1H:0} moderate, need ≥{config.MinimumAdx:0}";

    if (marketBias == TrendDirection.Buy && !RsiConfirmsLong(rsi1H, config))
      return $"Wait — 1H RSI(28) {rsi1H:0} not > {config.RsiBullThreshold:0}";

    if (marketBias == TrendDirection.Sell && !RsiConfirmsShort(rsi1H, config))
      return $"Wait — 1H RSI(28) {rsi1H:0} not < {config.RsiBearThreshold:0}";

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

  public static bool RsiConfirmsLong(decimal rsi1H, StrategyConfig config) =>
    rsi1H > config.RsiBullThreshold;

  public static bool RsiConfirmsShort(decimal rsi1H, StrategyConfig config) =>
    rsi1H < config.RsiBearThreshold;
}
