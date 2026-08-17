namespace PgAiTrading.Models;

/// <summary>
/// Intraday trade framework:
/// Market Structure → RSI+ADX regime → Volume Profile → Liquidity Sweep → Footprint → 5M BOS.
/// Chart-only (not framework gates): TPO display, Camarilla, CPR.
/// </summary>
public static class TradeFrameworkEvaluator
{
  public static MarketRegime GetRegime(decimal rsi1H, decimal adx1H, StrategyConfig config)
  {
    if (rsi1H > config.RsiBullThreshold)
      return MarketRegime.TrendingBullish;

    if (rsi1H < config.RsiBearThreshold)
      return MarketRegime.TrendingBearish;

    // RSI 45–55 — momentum neutral; ADX decides chop vs developing.
    if (adx1H < config.AdxWeakThreshold)
      return MarketRegime.StrongChop;

    if (adx1H > config.AdxDevelopingThreshold)
      return MarketRegime.DevelopingTrend;

    return MarketRegime.SoftNeutral;
  }

  public static string RegimeLabel(MarketRegime regime) => regime switch
  {
    MarketRegime.TrendingBullish => "Trending bullish",
    MarketRegime.TrendingBearish => "Trending bearish",
    MarketRegime.StrongChop => "Strong chop — sweep mean-reversion",
    MarketRegime.DevelopingTrend => "Developing — wait structure",
    MarketRegime.SoftNeutral => "Soft neutral",
    _ => "Unknown"
  };

  /// <summary>
  /// Direction from 1H market structure (primary), optionally aligned with 1H ST + VWAP.
  /// </summary>
  public static TrendDirection GetMarketBias(
    MultiTimeframeStructure structure,
    TrendDirection trend1H,
    bool aboveVwap)
  {
    var fromStructure = structure.MajorDirection;
    if (fromStructure == TrendDirection.Neutral)
      return TrendDirection.Neutral;

    // Prefer structure when ST/VWAP agree or ST is neutral; block when they hard-oppose.
    if (trend1H == TrendDirection.Buy && !aboveVwap && fromStructure == TrendDirection.Buy)
      return TrendDirection.Neutral;

    if (trend1H == TrendDirection.Sell && aboveVwap && fromStructure == TrendDirection.Sell)
      return TrendDirection.Neutral;

    if (trend1H != TrendDirection.Neutral && trend1H != fromStructure)
      return TrendDirection.Neutral;

    return fromStructure;
  }

  /// <summary>Legacy ST+VWAP bias kept for display / soft alignment.</summary>
  public static TrendDirection GetSuperTrendVwapBias(TrendDirection trend1H, bool aboveVwap) =>
    trend1H switch
    {
      TrendDirection.Buy when aboveVwap => TrendDirection.Buy,
      TrendDirection.Sell when !aboveVwap => TrendDirection.Sell,
      _ => TrendDirection.Neutral
    };

  public static bool IsStrongChopRegime(MarketRegime regime) =>
    regime == MarketRegime.StrongChop;

  /// <summary>Momentum-neutral RSI band (45–55). Prefer <see cref="GetRegime"/> for trade decisions.</summary>
  public static bool IsMomentumNeutral(decimal rsi1H, StrategyConfig config) =>
    rsi1H >= config.RsiBearThreshold && rsi1H <= config.RsiBullThreshold;

  /// <summary>
  /// Legacy helper: RSI mid-band only. Framework now uses <see cref="GetRegime"/> —
  /// StrongChop requires ADX &lt; 18 as well.
  /// </summary>
  public static bool IsRangebound(decimal rsi1H, StrategyConfig config) =>
    IsMomentumNeutral(rsi1H, config);

  public static bool IsSoftNoTradeRegime(MarketRegime regime) =>
    regime is MarketRegime.StrongChop or MarketRegime.SoftNeutral;

  public static bool IsRotationRegime(
    decimal adx1H,
    decimal price,
    VolumeProfileLevels profile,
    StrategyConfig config) =>
    adx1H < config.AdxWeakThreshold
    && profile.HasData
    && profile.IsInsideValueArea(price);

  public static bool IsRsiOversold(decimal rsi5M, StrategyConfig config) =>
    rsi5M < config.RsiReversalThreshold;

  public static bool ShouldWaitForReversal(decimal rsi5M, bool hasBullishPattern, StrategyConfig config) =>
    IsRsiOversold(rsi5M, config) && hasBullishPattern;

  public static TrendDirection GetTradeDirection(
    TrendDirection marketBias,
    MultiTimeframeStructure structure,
    MarketRegime regime,
    decimal adx1H,
    decimal price,
    VolumeProfileLevels profile,
    LiquiditySweepAnalysis sweep,
    StrategyConfig config)
  {
    // Strong chop: only mean-reversion after confirmed liquidity sweep.
    if (regime == MarketRegime.StrongChop)
    {
      if (!sweep.IsConfirmedSetup)
        return TrendDirection.Neutral;

      return sweep.ImpliedDirection;
    }

    // Soft neutral mid-RSI without developing ADX: no directional chase.
    if (regime == MarketRegime.SoftNeutral)
      return TrendDirection.Neutral;

    // Developing (RSI mid + ADX > 22): require 1H structure + 15M BOS alignment.
    if (regime == MarketRegime.DevelopingTrend)
    {
      if (marketBias == TrendDirection.Neutral)
        return TrendDirection.Neutral;

      if (!structure.Structure15M.Confirms(marketBias)
          && !HasAligned15MBos(structure.Structure15M, marketBias))
        return TrendDirection.Neutral;

      if (!profile.ConfirmsBuy(price) && marketBias == TrendDirection.Buy)
        return TrendDirection.Neutral;

      if (!profile.ConfirmsSell(price) && marketBias == TrendDirection.Sell)
        return TrendDirection.Neutral;

      return marketBias;
    }

    // Trending regimes.
    if (marketBias == TrendDirection.Neutral)
      return TrendDirection.Neutral;

    if (regime == MarketRegime.TrendingBullish && marketBias != TrendDirection.Buy)
      return TrendDirection.Neutral;

    if (regime == MarketRegime.TrendingBearish && marketBias != TrendDirection.Sell)
      return TrendDirection.Neutral;

    if (!HasAligned15MBos(structure.Structure15M, marketBias)
        && !structure.Structure15M.Confirms(marketBias))
      return TrendDirection.Neutral;

    if (adx1H < config.AdxWeakThreshold)
      return TrendDirection.Neutral;

    if (adx1H < config.MinimumAdx)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Buy && !profile.ConfirmsBuy(price))
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Sell && !profile.ConfirmsSell(price))
      return TrendDirection.Neutral;

    if (adx1H >= config.AdxStrongThreshold)
    {
      if (marketBias == TrendDirection.Buy && !profile.IsAboveValueArea(price)
          && !sweep.Confirms(TrendDirection.Buy))
        return TrendDirection.Neutral;

      if (marketBias == TrendDirection.Sell && !profile.IsBelowValueArea(price)
          && !sweep.Confirms(TrendDirection.Sell))
        return TrendDirection.Neutral;
    }

    return marketBias;
  }

  private static bool HasAligned15MBos(MarketStructureAnalysis structure15M, TrendDirection bias) =>
    bias switch
    {
      TrendDirection.Buy => structure15M.BosBullish
        || structure15M.LatestEvent is StructureEvent.BosBullish or StructureEvent.ChochBullish,
      TrendDirection.Sell => structure15M.BosBearish
        || structure15M.LatestEvent is StructureEvent.BosBearish or StructureEvent.ChochBearish,
      _ => false
    };

  /// <summary>Entry: 5M BOS/CHOCH in trade direction. SuperTrend (7,2.5) is trailing stop, not the entry gate.</summary>
  public static bool EntryTriggered(
    TrendDirection tradeDirection,
    MarketStructureAnalysis structure5M,
    TrendDirection trend5MEntry)
  {
    if (tradeDirection == TrendDirection.Neutral)
      return false;

    // trend5MEntry retained for callers/UI; entry gate is 5M structure break.
    _ = trend5MEntry;

    return tradeDirection switch
    {
      TrendDirection.Buy => structure5M.BosBullish
        || structure5M.LatestEvent is StructureEvent.BosBullish or StructureEvent.ChochBullish,
      TrendDirection.Sell => structure5M.BosBearish
        || structure5M.LatestEvent is StructureEvent.BosBearish or StructureEvent.ChochBearish,
      _ => false
    };
  }

  public static bool FootprintConfirmed(TrendDirection tradeDirection, FootprintAnalysis footprint) =>
    tradeDirection != TrendDirection.Neutral && footprint.Confirms(tradeDirection);

  public static bool IsFrameworkReady(
    TrendDirection tradeDirection,
    MarketStructureAnalysis structure5M,
    TrendDirection trend5MEntry,
    FootprintAnalysis footprint,
    LiquiditySweepAnalysis sweep,
    MarketRegime regime,
    bool waitForReversal)
  {
    if (waitForReversal || tradeDirection == TrendDirection.Neutral)
      return false;

    if (!EntryTriggered(tradeDirection, structure5M, trend5MEntry))
      return false;

    if (!FootprintConfirmed(tradeDirection, footprint))
      return false;

    // Strong chop requires a confirmed liquidity-sweep setup.
    if (regime == MarketRegime.StrongChop && !sweep.Confirms(tradeDirection))
      return false;

    // Soft neutral is not a directional trade regime.
    if (regime == MarketRegime.SoftNeutral)
      return false;

    return true;
  }

  public static int CalculateScore(
    TrendDirection marketBias,
    TrendDirection tradeDirection,
    MultiTimeframeStructure structure,
    TrendDirection trend1H,
    TrendDirection trend15M,
    TrendDirection trend5MEntry,
    TrendStrength strength1H,
    bool aboveVwap,
    FootprintAnalysis footprint,
    VolumeProfileLevels profile,
    decimal price,
    LiquiditySweepAnalysis sweep,
    MarketRegime regime,
    bool frameworkReady)
  {
    var score = 25;

    if (structure.Structure1H.Bias is StructureBias.Bullish or StructureBias.Bearish)
      score += 12;
    else if (structure.Structure1H.Bias == StructureBias.Mixed)
      score += 3;

    if (marketBias != TrendDirection.Neutral) score += 8;
    if (tradeDirection != TrendDirection.Neutral) score += 12;

    var alignment = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (alignment != TrendDirection.Neutral)
    {
      if (structure.Structure1H.Confirms(alignment)) score += 8;
      if (HasAligned15MBos(structure.Structure15M, alignment) || structure.Structure15M.Confirms(alignment))
        score += 10;
      if (EntryTriggered(alignment, structure.Structure5M, trend5MEntry)) score += 10;

      if (alignment == TrendDirection.Buy && aboveVwap) score += 4;
      if (alignment == TrendDirection.Sell && !aboveVwap) score += 4;

      if (trend1H == alignment) score += 3;
      if (trend15M == alignment) score += 3;
    }

    score += strength1H switch
    {
      TrendStrength.Strong => 12,
      TrendStrength.Moderate => 6,
      _ => 0
    };

    if (profile.HasData)
    {
      if (tradeDirection == TrendDirection.Buy && profile.ConfirmsBuy(price)) score += 8;
      else if (tradeDirection == TrendDirection.Sell && profile.ConfirmsSell(price)) score += 8;
      else if (marketBias == TrendDirection.Buy && profile.ConfirmsBuy(price)) score += 4;
      else if (marketBias == TrendDirection.Sell && profile.ConfirmsSell(price)) score += 4;
    }

    if (sweep.IsConfirmedSetup && sweep.ImpliedDirection == tradeDirection) score += 12;
    else if (sweep.Detected && sweep.Reclaimed) score += 5;

    if (footprint.Confirms(tradeDirection)) score += 12;
    else if (tradeDirection != TrendDirection.Neutral && (footprint.PositiveDelta || footprint.NegativeDelta))
      score += 4;

    if (frameworkReady) score += 10;

    var primaryBias = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (primaryBias != TrendDirection.Neutral
        && FootprintDisplayHelper.FootprintOpposesBias(footprint, primaryBias))
    {
      score -= 22;
      score = Math.Min(score, 68);
    }

    if (tradeDirection != TrendDirection.Neutral && !footprint.Confirms(tradeDirection))
      score = Math.Min(score, 82);

    if (tradeDirection != TrendDirection.Neutral
        && !EntryTriggered(tradeDirection, structure.Structure5M, trend5MEntry))
      score = Math.Min(score, 78);

    if (!frameworkReady)
      score = Math.Min(score, 85);

    if (frameworkReady)
      score = Math.Max(score, 75);

    if (regime == MarketRegime.StrongChop && !sweep.IsConfirmedSetup)
      score = Math.Min(score, 48);
    else if (regime == MarketRegime.SoftNeutral)
      score = Math.Min(score, 52);
    else if (regime == MarketRegime.DevelopingTrend && tradeDirection == TrendDirection.Neutral)
      score = Math.Min(score, 58);

    return Math.Clamp(score, 0, 99);
  }

  public static string GetScoreStrengthLabel(
    int score,
    MarketRegime regime,
    bool frameworkReady,
    bool footprintConflict = false,
    bool sweepSetup = false)
  {
    if (frameworkReady)
      return "Ready";

    if (footprintConflict)
      return "Flow Conflict";

    if (regime == MarketRegime.StrongChop)
      return sweepSetup ? "Sweep Setup" : "Strong Chop";

    if (regime == MarketRegime.DevelopingTrend)
      return "Developing";

    if (regime == MarketRegime.SoftNeutral)
      return "Soft Neutral";

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
    MultiTimeframeStructure structure,
    TrendDirection trend1H,
    TrendDirection trend5MEntry,
    decimal adx1H,
    decimal rsi1H,
    bool aboveVwap,
    FootprintAnalysis footprint,
    VolumeProfileLevels profile,
    decimal price,
    LiquiditySweepAnalysis sweep,
    MarketRegime regime,
    bool waitForReversal,
    StrategyConfig config)
  {
    if (waitForReversal)
      return "Wait — 5m RSI oversold + bullish pattern";

    if (regime == MarketRegime.StrongChop)
    {
      if (!sweep.Detected)
        return "Strong chop — wait liquidity sweep at VA/PDH/PDL";
      if (!sweep.Reclaimed)
        return $"Sweep of {sweep.LevelName} — wait reclaim";
      if (!sweep.StructureConfirmed)
        return "Sweep reclaimed — wait 5M BOS";
      if (!footprint.Confirms(tradeDirection == TrendDirection.Neutral ? sweep.ImpliedDirection : tradeDirection))
        return "Wait — footprint not confirmed on sweep";
    }

    if (regime == MarketRegime.SoftNeutral)
      return "Soft neutral — RSI mid + ADX not developing";

    if (structure.Structure1H.Bias == StructureBias.Insufficient)
      return "Wait — 1H market structure insufficient";

    if (structure.Structure1H.Bias == StructureBias.Mixed
        && regime is not MarketRegime.DevelopingTrend)
      return "Wait — 1H structure mixed/chop";

    if (marketBias == TrendDirection.Neutral)
    {
      if (structure.MajorDirection != TrendDirection.Neutral
          && trend1H != TrendDirection.Neutral
          && trend1H != structure.MajorDirection)
        return "Wait — 1H structure vs SuperTrend conflict";

      if (structure.MajorDirection == TrendDirection.Buy && trend1H == TrendDirection.Buy && !aboveVwap)
        return "Step 1 — bullish structure but price below session VWAP";

      if (structure.MajorDirection == TrendDirection.Sell && trend1H == TrendDirection.Sell && aboveVwap)
        return "Step 1 — bearish structure but price above session VWAP";

      return "Wait — 1H market structure not directional";
    }

    if (regime == MarketRegime.DevelopingTrend
        && !HasAligned15MBos(structure.Structure15M, marketBias)
        && !structure.Structure15M.Confirms(marketBias))
      return "Developing — wait 15M BOS with 1H structure";

    if (adx1H < config.AdxWeakThreshold && regime != MarketRegime.StrongChop)
      return $"Wait — ADX {adx1H:0} choppy (<{config.AdxWeakThreshold:0})";

    if (regime is MarketRegime.TrendingBullish or MarketRegime.TrendingBearish
        && adx1H < config.MinimumAdx)
      return $"Wait — ADX {adx1H:0} moderate, need ≥{config.MinimumAdx:0}";

    if (tradeDirection == TrendDirection.Neutral
        && !HasAligned15MBos(structure.Structure15M, marketBias)
        && !structure.Structure15M.Confirms(marketBias))
      return "Wait — 15M structure/BOS not aligned";

    if (marketBias == TrendDirection.Buy && !profile.ConfirmsBuy(price))
      return "Wait — price not above POC (volume profile)";

    if (marketBias == TrendDirection.Sell && !profile.ConfirmsSell(price))
      return "Wait — price not below POC (volume profile)";

    if (!EntryTriggered(tradeDirection == TrendDirection.Neutral ? marketBias : tradeDirection,
          structure.Structure5M, trend5MEntry))
      return "Wait — 5M BOS not triggered";

    var dir = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (!footprint.Confirms(dir))
      return "Wait — footprint not confirmed";

    return "Ready";
  }

  public static bool RsiConfirmsLong(decimal rsi1H, StrategyConfig config) =>
    rsi1H > config.RsiBullThreshold;

  public static bool RsiConfirmsShort(decimal rsi1H, StrategyConfig config) =>
    rsi1H < config.RsiBearThreshold;

  // ---- Compatibility shims for older call sites / tests ----

  public static TrendDirection GetMarketBias(TrendDirection trend1H, bool aboveVwap) =>
    GetSuperTrendVwapBias(trend1H, aboveVwap);

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
    var regime = GetRegime(rsi1H, adx1H, config);
    if (regime is MarketRegime.StrongChop or MarketRegime.SoftNeutral)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Neutral)
      return TrendDirection.Neutral;

    if (trend15M != marketBias)
      return TrendDirection.Neutral;

    if (adx1H < config.AdxWeakThreshold || adx1H < config.MinimumAdx)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Buy && !RsiConfirmsLong(rsi1H, config)
        && regime != MarketRegime.DevelopingTrend)
      return TrendDirection.Neutral;

    if (marketBias == TrendDirection.Sell && !RsiConfirmsShort(rsi1H, config)
        && regime != MarketRegime.DevelopingTrend)
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
    if (frameworkReady) score += 10;

    var primaryBias = tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias;
    if (primaryBias != TrendDirection.Neutral
        && FootprintDisplayHelper.FootprintOpposesBias(footprint, primaryBias))
      score = Math.Min(score - 22, 68);

    if (!frameworkReady) score = Math.Min(score, 85);
    if (frameworkReady) score = Math.Max(score, 75);
    if (isRotationRegime) score = Math.Min(score, 48);
    else if (isRangebound) score = Math.Min(score, 54);

    return Math.Clamp(score, 0, 99);
  }

  public static string GetScoreStrengthLabel(
    int score,
    bool isRangebound,
    bool isRotationRegime,
    bool frameworkReady,
    bool footprintConflict = false)
  {
    if (frameworkReady) return "Ready";
    if (footprintConflict) return "Flow Conflict";
    if (isRotationRegime) return "Rotation";
    if (isRangebound) return "Range-bound";
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
      return "Wait — 5m RSI oversold + bullish pattern";

    if (isRotationRegime)
      return "Rotation inside VA — avoid breakouts";

    var regime = GetRegime(rsi1H, adx1H, config);
    if (regime == MarketRegime.StrongChop)
      return "Strong chop — wait liquidity sweep at VA/PDH/PDL";

    if (regime == MarketRegime.SoftNeutral || isRangebound)
      return "Soft neutral — RSI mid; wait structure or ADX develop";

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

    if (marketBias == TrendDirection.Buy && !RsiConfirmsLong(rsi1H, config)
        && regime != MarketRegime.DevelopingTrend)
      return $"Wait — 1H RSI(28) {rsi1H:0} not > {config.RsiBullThreshold:0}";

    if (marketBias == TrendDirection.Sell && !RsiConfirmsShort(rsi1H, config)
        && regime != MarketRegime.DevelopingTrend)
      return $"Wait — 1H RSI(28) {rsi1H:0} not < {config.RsiBearThreshold:0}";

    if (!tpo.Confirms(marketBias))
      return $"Wait — POC: {tpo.Summary}";

    if (trend5MEntry != marketBias)
      return "Wait — 5m entry SuperTrend (7,2.5) not triggered";

    if (!footprint.Confirms(marketBias))
      return "Wait — footprint not confirmed";

    return "Ready";
  }
}
