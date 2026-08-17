using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface ISignalService
{
    Task<Signal> GenerateSignalAsync(string instrument = "NIFTY");
    Task<Signal> GenerateSignalFromAnalysisAsync(string instrument, MultiTimeframeAnalysis analysis);
    Task<MultiTimeframeAnalysis> AnalyzeAsync(string instrument = "NIFTY", string? chartTimeframe = null);
    Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument);
    /// <summary>Phase-1 intraday screen: 1H + 5m only. Returns prefetch when Step 1 passes (bullish bias).</summary>
    Task<IntradayPrefetch?> TryScreenIntradayPhase1Async(string instrument);
    Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument, IntradayPrefetch prefetch);
}

public class SignalService : ISignalService
{
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly IIndicatorService _indicators;
    private readonly ISettingsService _settings;
    private readonly IFootprintService _footprint;
    private readonly IVolumeProfileService _volumeProfile;
    private readonly IChartPatternService _patterns;
    private readonly IMarketStructureService _structure;

    public SignalService(
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        ISettingsService settings,
        IFootprintService footprint,
        IVolumeProfileService volumeProfile,
        IChartPatternService patterns,
        IMarketStructureService structure)
    {
        _marketData = marketData;
        _superTrend = superTrend;
        _indicators = indicators;
        _settings = settings;
        _footprint = footprint;
        _volumeProfile = volumeProfile;
        _patterns = patterns;
        _structure = structure;
    }

    public async Task<MultiTimeframeAnalysis> AnalyzeAsync(string instrument = "NIFTY", string? chartTimeframe = null)
        => await AnalyzeWithConfigAsync(instrument, _settings.Strategy, chartTimeframe: chartTimeframe);

    public Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument)
        => AnalyzeWithConfigAsync(instrument, FrameworkDefaults.Intraday);

    public Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument, IntradayPrefetch prefetch)
        => AnalyzeWithConfigAsync(instrument, FrameworkDefaults.Intraday, prefetch);

    public async Task<IntradayPrefetch?> TryScreenIntradayPhase1Async(string instrument)
    {
        var config = FrameworkDefaults.Intraday;
        var symbolKey = MapInstrument(instrument);

        var candles1H = await _marketData.GetCandlesAsync(symbolKey, "1H", 200);
        var candles5M = await _marketData.GetCandlesAsync(symbolKey, "5m", 200);
        var candles15M = await _marketData.GetCandlesAsync(symbolKey, "15m", 200);
        if (candles1H.Count < 30 || candles5M.Count < 30)
            return null;

        var structure = _structure.AnalyzeMulti(candles1H, candles15M, candles5M);
        if (structure.MajorDirection != TrendDirection.Buy)
            return null;

        var trend1H = _superTrend.GetTrend(candles1H, config.SuperTrend1HPeriod, config.SuperTrend1HMultiplier);
        var vwap5M = candles5M.Count > 0
            ? candles5M[^1].Vwap ?? candles5M.LastOrDefault(c => c.Vwap.HasValue)?.Vwap ?? 0m
            : 0m;
        var last5MClose = candles5M[^1].Close;
        var aboveVwap = vwap5M > 0 && last5MClose >= vwap5M;
        var marketBias = TradeFrameworkEvaluator.GetMarketBias(structure, trend1H, aboveVwap);
        if (marketBias != TrendDirection.Buy)
            return null;

        var rsi5M = _indicators.CalculateRsi(candles5M, config.RsiLength);
        var hasBullishPattern = _patterns.TryGetLatestBullishPattern(candles5M, out _);
        if (TradeFrameworkEvaluator.ShouldWaitForReversal(rsi5M, hasBullishPattern, config))
            return null;

        var rsiTrend = _indicators.CalculateRsi(candles1H, config.RsiTrendLength);
        var adx1H = _indicators.CalculateAdx(candles1H, config.AdxLength);
        var regime = TradeFrameworkEvaluator.GetRegime(rsiTrend, adx1H, config);

        // Soft neutral is not a long-scan candidate; strong chop is handled in phase 2 via sweeps.
        if (regime == MarketRegime.SoftNeutral)
            return null;

        return new IntradayPrefetch
        {
            Symbol = instrument.ToUpperInvariant(),
            InstrumentKey = symbolKey,
            Candles1H = candles1H,
            Candles5M = candles5M
        };
    }

    private async Task<MultiTimeframeAnalysis> AnalyzeWithConfigAsync(
        string instrument,
        StrategyConfig config,
        IntradayPrefetch? prefetch = null,
        string? chartTimeframe = null)
    {
        var symbol = MapInstrument(instrument);

        var candles1H = prefetch?.Candles1H ?? await _marketData.GetCandlesAsync(symbol, "1H", 200);
        var candles5M = prefetch?.Candles5M ?? await _marketData.GetCandlesAsync(symbol, "5m", 200);
        var candles15M = await _marketData.GetCandlesAsync(symbol, "15m", 200);
        // Extra daily history so weekly/monthly Camarilla Auto periods have enough bars.
        var dailyCount = CamarillaCalculator.ResolvePivotTimeframe(chartTimeframe) switch
        {
            "1M" => 120,
            "1W" => 60,
            _ => 15
        };
        var candlesDay = await _marketData.GetCandlesAsync(symbol, "1D", dailyCount);

        var trend1H = _superTrend.GetTrend(candles1H, config.SuperTrend1HPeriod, config.SuperTrend1HMultiplier);
        var trend15M = _superTrend.GetTrend(candles15M, config.SuperTrend15MPeriod, config.SuperTrend15MMultiplier);
        var trend5M = _superTrend.GetTrend(candles5M, config.SuperTrend5MPeriod, config.SuperTrend5MMultiplier);
        var trend5MEntry = _superTrend.GetTrend(
            candles5M,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier);

        var rsiTrend = _indicators.CalculateRsi(candles1H, config.RsiTrendLength);
        var rsi = _indicators.CalculateRsi(candles1H, config.RsiLength);
        var rsi15M = _indicators.CalculateRsi(candles15M, config.RsiLength);
        var rsi5M = _indicators.CalculateRsi(candles5M, config.RsiLength);

        var rsiBias = TradeFrameworkEvaluator.RsiConfirmsLong(rsiTrend, config) ? TrendDirection.Buy
            : TradeFrameworkEvaluator.RsiConfirmsShort(rsiTrend, config) ? TrendDirection.Sell
            : TrendDirection.Neutral;

        var adx1H = _indicators.CalculateAdx(candles1H, config.AdxLength);
        var strength1H = adx1H < config.AdxWeakThreshold ? TrendStrength.Weak
            : adx1H < config.AdxStrongThreshold ? TrendStrength.Moderate
            : TrendStrength.Strong;

        string? reversalReason = null;
        var hasBullishPattern = _patterns.TryGetLatestBullishPattern(candles5M, out var bullishPatternLabel);
        var rsiOversold = TradeFrameworkEvaluator.IsRsiOversold(rsi5M, config);
        var waitForReversal = TradeFrameworkEvaluator.ShouldWaitForReversal(rsi5M, hasBullishPattern, config);
        var expectReversal = rsiOversold && !waitForReversal;
        if (waitForReversal)
        {
            reversalReason = string.IsNullOrWhiteSpace(bullishPatternLabel)
                ? $"5m RSI {rsi5M:0} < {config.RsiReversalThreshold:0} + bullish pattern"
                : $"5m RSI {rsi5M:0} < {config.RsiReversalThreshold:0} + {bullishPatternLabel}";
        }
        else if (expectReversal)
        {
            reversalReason = $"5m RSI {rsi5M:0} < {config.RsiReversalThreshold:0} — expect reversal";
        }

        var vwap5M = candles5M.Count > 0
            ? candles5M[^1].Vwap ?? candles5M.LastOrDefault(c => c.Vwap.HasValue)?.Vwap ?? 0m
            : 0m;
        var last5MClose = candles5M.Count > 0 ? candles5M[^1].Close : 0m;
        var aboveVwap = vwap5M > 0 && last5MClose >= vwap5M;

        var cprCandles = candlesDay.Count >= 2 ? candlesDay : candles1H;
        var cprAnalysis = _indicators.GetCprAnalysis(cprCandles);

        var sessionCandles = GetTodaySessionCandles(candles5M);
        var prevSessionCandles = GetPreviousSessionCandles(candles5M);
        var volumeProfile = _volumeProfile.BuildLevels(sessionCandles, prevSessionCandles);
        var sessionOpen = sessionCandles.Count > 0 ? sessionCandles[0].Open : 0m;

        // Camarilla / CPR: chart display only (not framework gates).
        var chartCandlesForCam = (chartTimeframe ?? "5m") switch
        {
            "1W" => candlesDay,
            "1D" => candlesDay,
            "1H" => candles1H,
            "15m" => candles15M,
            _ => candles5M
        };
        var camarilla = CamarillaCalculator.ForChartTimeframe(
            chartTimeframe ?? "5m",
            chartCandlesForCam,
            candlesDay,
            prevSessionCandles,
            last5MClose,
            MarketHours.GetIstNow().Date);

        // Volume-profile POC confirm (legacy Tpo* fields kept for UI compatibility).
        var tpo = TpoConfirmationEvaluator.Evaluate(
            last5MClose,
            sessionOpen,
            volumeProfile,
            adx1H,
            cprNarrow: false,
            config);

        var mtfStructure = _structure.AnalyzeMulti(candles1H, candles15M, candles5M);
        var regime = TradeFrameworkEvaluator.GetRegime(rsiTrend, adx1H, config);
        var isRotationRegime = regime == MarketRegime.StrongChop
            || TradeFrameworkEvaluator.IsRotationRegime(adx1H, last5MClose, volumeProfile, config);
        var isRangebound = regime is MarketRegime.StrongChop or MarketRegime.SoftNeutral;

        var marketBias = TradeFrameworkEvaluator.GetMarketBias(mtfStructure, trend1H, aboveVwap);

        var instrumentKey = MapInstrument(instrument);
        var (footprintCandles, volumeSource, futuresSymbol) = await _marketData.GetFootprintCandlesAsync(instrumentKey, candles5M);
        var footprint = _footprint.Analyze(footprintCandles, volumeSource, futuresSymbol);

        var sweep = LiquiditySweepEvaluator.Evaluate(
            candles5M,
            volumeProfile,
            mtfStructure.Structure15M,
            mtfStructure.Structure5M,
            footprint);

        var tradeDirection = TradeFrameworkEvaluator.GetTradeDirection(
            marketBias,
            mtfStructure,
            regime,
            adx1H,
            last5MClose,
            volumeProfile,
            sweep,
            config);

        var entryTriggered = TradeFrameworkEvaluator.EntryTriggered(
            tradeDirection,
            mtfStructure.Structure5M,
            trend5MEntry);
        var tpoConfirmed = tradeDirection != TrendDirection.Neutral && tpo.Confirms(tradeDirection);
        var footprintConfirmed = TradeFrameworkEvaluator.FootprintConfirmed(tradeDirection, footprint);
        footprint.Summary = FootprintDisplayHelper.GetDisplayLabel(footprint, footprintConfirmed);
        var frameworkReady = TradeFrameworkEvaluator.IsFrameworkReady(
            tradeDirection,
            mtfStructure.Structure5M,
            trend5MEntry,
            footprint,
            sweep,
            regime,
            waitForReversal);

        var frameworkStatus = TradeFrameworkEvaluator.GetBlockingReason(
            marketBias,
            tradeDirection,
            mtfStructure,
            trend1H,
            trend5MEntry,
            adx1H,
            rsiTrend,
            aboveVwap,
            footprint,
            volumeProfile,
            last5MClose,
            sweep,
            regime,
            waitForReversal,
            config);

        if (frameworkStatus == "Wait — footprint not confirmed" && tradeDirection != TrendDirection.Neutral)
            frameworkStatus = FootprintDisplayHelper.GetStep4BlockingDetail(footprint, tradeDirection);

        var score = TradeFrameworkEvaluator.CalculateScore(
            marketBias,
            tradeDirection,
            mtfStructure,
            trend1H,
            trend15M,
            trend5MEntry,
            strength1H,
            aboveVwap,
            footprint,
            volumeProfile,
            last5MClose,
            sweep,
            regime,
            frameworkReady);

        return new MultiTimeframeAnalysis
        {
            Trend1H = trend1H,
            Trend15M = trend15M,
            Trend5M = trend5M,
            Trend5MEntry = trend5MEntry,
            Rsi = rsi,
            RsiTrend = rsiTrend,
            RsiBias = rsiBias,
            Adx = adx1H,
            Strength1H = strength1H,
            Cpr = cprAnalysis.Bias,
            CprNarrow = cprAnalysis.IsNarrow,
            CprWidthPercent = cprAnalysis.WidthPercent,
            CprPivot = cprAnalysis.Pivot,
            CprTc = cprAnalysis.Top,
            CprBc = cprAnalysis.Bottom,
            Vwap5M = vwap5M,
            AboveVwap = aboveVwap,
            IsRangebound = isRangebound,
            IsRotationRegime = isRotationRegime,
            Regime = regime,
            Structure = mtfStructure,
            LiquiditySweep = sweep,
            ExpectReversal = expectReversal,
            WaitForReversal = waitForReversal,
            ReversalReason = reversalReason,
            BullishPatternLabel = bullishPatternLabel,
            Rsi5M = rsi5M,
            Rsi15M = rsi15M,
            MarketBias = marketBias,
            TradeDirection = tradeDirection,
            EntryTriggered = entryTriggered,
            PocBias = tpo.Bias,
            SessionVaBias = volumeProfile.GetSessionValueAreaBias(last5MClose),
            PrevDayVaBias = volumeProfile.GetPrevDayValueAreaBias(last5MClose),
            TpoConfirmed = tpoConfirmed,
            FootprintConfirmed = footprintConfirmed,
            FrameworkReady = frameworkReady,
            FrameworkStatus = frameworkStatus,
            Tpo = tpo,
            Footprint = footprint,
            VolumeProfile = volumeProfile,
            Camarilla = camarilla,
            CamarillaBias = camarilla.GetBias(last5MClose),
            CamarillaBandBias = camarilla.GetBandBias(last5MClose),
            ReferencePrice = last5MClose,
            OverallScore = score,
            Strength = TradeFrameworkEvaluator.GetScoreStrengthLabel(
                score,
                regime,
                frameworkReady,
                FootprintDisplayHelper.FootprintOpposesBias(
                    footprint,
                    tradeDirection != TrendDirection.Neutral ? tradeDirection : marketBias),
                sweep.IsConfirmedSetup)
        };
    }

    public async Task<Signal> GenerateSignalAsync(string instrument = "NIFTY")
    {
        var analysis = await AnalyzeAsync(instrument);
        return await GenerateSignalFromAnalysisAsync(instrument, analysis);
    }

    public async Task<Signal> GenerateSignalFromAnalysisAsync(string instrument, MultiTimeframeAnalysis analysis)
    {
        var price = await _marketData.GetCurrentPriceAsync(MapInstrument(instrument));
        var strikeStep = GetStrikeStep(instrument);
        var strike = (int)(Math.Round(price / strikeStep) * strikeStep);
        var candles5M = await _marketData.GetCandlesAsync(MapInstrument(instrument), "5m", 200);
        var stopLoss = BuildStopLoss(instrument, analysis.TradeDirection, candles5M);

        var reasons = new List<string>
        {
            $"Structure — 1H {analysis.Structure.Structure1H.Summary}",
            $"Regime — {TradeFrameworkEvaluator.RegimeLabel(analysis.Regime)} (RSI {analysis.RsiTrend:0}, ADX {analysis.Adx:0})",
            $"15M — {analysis.Structure.Structure15M.Summary}",
            $"Volume profile — {analysis.Tpo.Summary}",
            $"Liquidity sweep — {analysis.LiquiditySweep.Summary}",
            $"Footprint — {analysis.Footprint.Summary}",
            $"5M entry — {analysis.Structure.Structure5M.Summary}; ST(7,2.5) {analysis.Trend5MEntry} → {(analysis.EntryTriggered ? "triggered" : "waiting")}"
        };

        if (analysis.WaitForReversal)
        {
            reasons.Insert(0, $"⚠ No new entry: {analysis.ReversalReason}");
            return NoTrade(instrument, "Wait — 5m RSI oversold + bullish pattern", analysis.OverallScore, reasons);
        }

        if (analysis.ExpectReversal)
        {
            reasons.Insert(0, $"⚠ Expect reversal: {analysis.ReversalReason}");
        }

        // Strong chop with confirmed sweep → allow mean-reversion directional signal path below.
        if (analysis.Regime == MarketRegime.StrongChop && !analysis.LiquiditySweep.IsConfirmedSetup)
        {
            reasons.Insert(0, "Strong chop — wait liquidity sweep at VA / PDH / PDL");
            return new Signal
            {
                Instrument = instrument,
                Trend = TrendDirection.Neutral,
                Entry = $"{strike} straddle/IC",
                Strategy = "Sweep mean-reversion at profile extremes",
                StopLoss = "Beyond swept level / Keltner (20,2)",
                Target = "VWAP / POC",
                Confidence = analysis.OverallScore,
                Reasons = reasons
            };
        }

        if (analysis.Regime == MarketRegime.SoftNeutral)
        {
            reasons.Insert(0, "Soft neutral — RSI mid + ADX not developing; wait 1H structure");
            return new Signal
            {
                Instrument = instrument,
                Trend = TrendDirection.Neutral,
                Entry = "-",
                Strategy = "Stand aside",
                StopLoss = "-",
                Target = "-",
                Confidence = analysis.OverallScore,
                Reasons = reasons
            };
        }

        if (!analysis.FrameworkReady)
        {
            reasons.Add($"✖ {analysis.FrameworkStatus}");
            return NoTrade(instrument, analysis.FrameworkStatus, analysis.OverallScore, reasons);
        }

        var bias = analysis.TradeDirection;
        // Index options: bullish → buy ATM CE; bearish → sell ATM CE (not PE).
        // Include Buy/Sell in Entry so the dock pill isn't just "63950 CE" next to Bearish.
        var (entry, strategy, optionType) = bias == TrendDirection.Buy
            ? ($"Buy {strike} CE", "Debit Spread", "CE")
            : ($"Sell {strike} CE", "Sell ATM Call", "CE");

        var target = analysis.VolumeProfile.TargetSummary(bias);

        return new Signal
        {
            Instrument = instrument,
            Trend = bias,
            Entry = entry,
            Strategy = strategy,
            OptionType = optionType,
            Strike = strike,
            StopLoss = stopLoss.Text,
            StopLossLevel = stopLoss.Level,
            Target = target,
            Confidence = analysis.OverallScore,
            Reasons = reasons
        };
    }

    private static List<Candle> GetTodaySessionCandles(List<Candle> candles5M)
    {
        if (candles5M.Count == 0)
            return candles5M;

        var today = MarketHours.GetIstNow().Date;
        return candles5M.Where(c => c.Timestamp.Date == today).ToList();
    }

    private static List<Candle>? GetPreviousSessionCandles(List<Candle> candles5M)
    {
        if (candles5M.Count == 0)
            return null;

        var today = MarketHours.GetIstNow().Date;
        var prev = candles5M.Where(c => c.Timestamp.Date < today).ToList();
        if (prev.Count == 0)
            return null;

        var lastDate = prev.Max(c => c.Timestamp.Date);
        return prev.Where(c => c.Timestamp.Date == lastDate).ToList();
    }

    private (string Text, decimal? Level) BuildStopLoss(string instrument, TrendDirection bias, List<Candle> candles5M)
    {
        var (_, superTrendValues) = _superTrend.Calculate(
            candles5M,
            TrailingStopDefaults.Period,
            TrailingStopDefaults.Multiplier);

        if (superTrendValues.Count == 0)
        {
            return (
                $"5m ST ({TrailingStopDefaults.Period}, {TrailingStopDefaults.Multiplier})",
                null);
        }

        var level = superTrendValues[^1];
        var direction = bias == TrendDirection.Sell
            ? $"exit if {instrument.ToUpperInvariant()} closes above"
            : $"exit if {instrument.ToUpperInvariant()} closes below";

        return (
            $"₹{level:N2} — 5m ST ({TrailingStopDefaults.Period}, {TrailingStopDefaults.Multiplier}) — {direction}",
            level);
    }

    private static int GetStrikeStep(string instrument) => instrument.ToUpperInvariant() switch
    {
        "BANKNIFTY" => 100,
        "SENSEX" => 100,
        _ => 50
    };

    private static Signal NoTrade(string instrument, string reason, int confidence, List<string> reasons) => new()
    {
        Instrument = instrument,
        Trend = TrendDirection.Neutral,
        Entry = "No Trade",
        Strategy = reason,
        StopLoss = "-",
        Target = "-",
        Confidence = confidence,
        Reasons = reasons
    };

    private static string BiasLabel(TrendDirection bias) => bias switch
    {
        TrendDirection.Buy => "Bullish",
        TrendDirection.Sell => "Bearish",
        _ => "Neutral"
    };

    private static string MapInstrument(string instrument) => InstrumentMapper.ToZerodhaKey(instrument);
}
