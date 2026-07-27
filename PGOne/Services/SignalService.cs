using PGOne.Models;

namespace PGOne.Services;

public interface ISignalService
{
    Task<Signal> GenerateSignalAsync(string instrument = "NIFTY");
    Task<MultiTimeframeAnalysis> AnalyzeAsync(string instrument = "NIFTY");
    Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument);
}

public class SignalService : ISignalService
{
    private readonly IMarketDataService _marketData;
    private readonly ISuperTrendService _superTrend;
    private readonly IIndicatorService _indicators;
    private readonly ISettingsService _settings;
    private readonly IFootprintService _footprint;
    private readonly IVolumeProfileService _volumeProfile;

    public SignalService(
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        ISettingsService settings,
        IFootprintService footprint,
        IVolumeProfileService volumeProfile)
    {
        _marketData = marketData;
        _superTrend = superTrend;
        _indicators = indicators;
        _settings = settings;
        _footprint = footprint;
        _volumeProfile = volumeProfile;
    }

    public async Task<MultiTimeframeAnalysis> AnalyzeAsync(string instrument = "NIFTY")
        => await AnalyzeWithConfigAsync(instrument, _settings.Strategy);

    public Task<MultiTimeframeAnalysis> AnalyzeForFrameworkAsync(string instrument)
        => AnalyzeWithConfigAsync(instrument, FrameworkDefaults.Intraday);

    private async Task<MultiTimeframeAnalysis> AnalyzeWithConfigAsync(string instrument, StrategyConfig config)
    {
        var symbol = MapInstrument(instrument);

        var candles1H = await _marketData.GetCandlesAsync(symbol, "1H", 200);
        var candles15M = await _marketData.GetCandlesAsync(symbol, "15m", 200);
        var candles5M = await _marketData.GetCandlesAsync(symbol, "5m", 200);
        var candlesDay = await _marketData.GetCandlesAsync(symbol, "1D", 10);

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
        if (rsi5M < config.RsiReversalThreshold)
            reversalReason = $"5m RSI {rsi5M:0} < {config.RsiReversalThreshold:0}";

        var vwap5M = candles5M.LastOrDefault(c => c.Vwap.HasValue)?.Vwap ?? 0m;
        var last5MClose = candles5M.Count > 0 ? candles5M[^1].Close : 0m;
        var aboveVwap = vwap5M > 0 && last5MClose >= vwap5M;

        var cprCandles = candlesDay.Count >= 2 ? candlesDay : candles1H;
        var cprAnalysis = _indicators.GetCprAnalysis(cprCandles);

        var sessionCandles = GetTodaySessionCandles(candles5M);
        var prevSessionCandles = GetPreviousSessionCandles(candles5M);
        var volumeProfile = _volumeProfile.BuildLevels(sessionCandles, prevSessionCandles);
        var sessionOpen = sessionCandles.Count > 0 ? sessionCandles[0].Open : 0m;

        var tpo = TpoConfirmationEvaluator.Evaluate(
            last5MClose,
            sessionOpen,
            volumeProfile,
            adx1H,
            cprAnalysis.IsNarrow,
            config);

        var isRotationRegime = TradeFrameworkEvaluator.IsRotationRegime(
            adx1H, last5MClose, volumeProfile, config);
        var isRangebound = TradeFrameworkEvaluator.IsRangebound(rsiTrend, config);

        var marketBias = TradeFrameworkEvaluator.GetMarketBias(trend1H, aboveVwap);
        var tradeDirection = TradeFrameworkEvaluator.GetTradeDirection(
            marketBias,
            trend15M,
            adx1H,
            rsiTrend,
            last5MClose,
            volumeProfile,
            tpo,
            config);

        var footprintBias = tradeDirection != TrendDirection.Neutral
            ? tradeDirection
            : marketBias != TrendDirection.Neutral ? marketBias : trend1H;

        var footprint = _footprint.Analyze(candles5M, footprintBias);

        var entryTriggered = TradeFrameworkEvaluator.EntryTriggered(tradeDirection, trend5MEntry);
        var tpoConfirmed = tradeDirection != TrendDirection.Neutral && tpo.Confirms(tradeDirection);
        var footprintConfirmed = TradeFrameworkEvaluator.FootprintConfirmed(tradeDirection, footprint);
        var frameworkReady = TradeFrameworkEvaluator.IsFrameworkReady(
            tradeDirection,
            trend5MEntry,
            footprint,
            reversalReason is not null,
            isRotationRegime,
            isRangebound);

        var frameworkStatus = TradeFrameworkEvaluator.GetBlockingReason(
            marketBias,
            tradeDirection,
            trend1H,
            trend15M,
            trend5MEntry,
            adx1H,
            rsiTrend,
            aboveVwap,
            footprint,
            tpo,
            reversalReason is not null,
            isRotationRegime,
            isRangebound,
            config);

        var score = TradeFrameworkEvaluator.CalculateScore(
            marketBias,
            tradeDirection,
            trend1H,
            trend15M,
            trend5MEntry,
            strength1H,
            aboveVwap,
            footprint,
            tpo,
            isRotationRegime,
            isRangebound,
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
            Vwap5M = vwap5M,
            AboveVwap = aboveVwap,
            IsRangebound = isRangebound,
            IsRotationRegime = isRotationRegime,
            WaitForReversal = reversalReason is not null,
            ReversalReason = reversalReason,
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
            OverallScore = score,
            Strength = strength1H.ToString()
        };
    }

    public async Task<Signal> GenerateSignalAsync(string instrument = "NIFTY")
    {
        var analysis = await AnalyzeAsync(instrument);
        var price = await _marketData.GetCurrentPriceAsync(MapInstrument(instrument));
        var strikeStep = GetStrikeStep(instrument);
        var strike = (int)(Math.Round(price / strikeStep) * strikeStep);
        var candles5M = await _marketData.GetCandlesAsync(MapInstrument(instrument), "5m", 200);
        var stopLoss = BuildStopLoss(instrument, analysis.TradeDirection, candles5M);

        var reasons = new List<string>
        {
            $"Step 1 — 1H ST {analysis.Trend1H}, VWAP {(analysis.AboveVwap ? "above" : "below")} → {BiasLabel(analysis.MarketBias)}",
            $"Step 2 — 15M ST {analysis.Trend15M}, ADX(1H) {analysis.Adx:0} ({analysis.Strength1H}), RSI(28) {analysis.RsiTrend:0} → {BiasLabel(analysis.TradeDirection)}",
            $"POC — {analysis.Tpo.Summary}{(analysis.Tpo.StrongTrendDay ? " (strong trend day)" : "")}",
            $"Step 3 — 5M entry ST (7,2.5) {analysis.Trend5MEntry} → {(analysis.EntryTriggered ? "triggered" : "waiting")}",
            $"Step 4 — Footprint: {analysis.Footprint.Summary}",
            $"CPR {analysis.Cpr}{(analysis.CprNarrow ? " narrow" : "")}"
        };

        if (analysis.WaitForReversal)
        {
            reasons.Insert(0, $"⚠ No new entry: {analysis.ReversalReason}");
            return NoTrade(instrument, "Wait — 5m RSI oversold", analysis.OverallScore, reasons);
        }

        if (analysis.IsRotationRegime)
        {
            reasons.Insert(0, "ADX < 18 inside Value Area — rotation, avoid breakouts");
            return new Signal
            {
                Instrument = instrument,
                Trend = TrendDirection.Neutral,
                Entry = $"{strike} straddle/IC",
                Strategy = "Keltner (20,1.5)/(20,2) fade + VWAP",
                StopLoss = "Beyond Keltner (20,2)",
                Target = "Mid / VWAP",
                Confidence = analysis.OverallScore,
                Reasons = reasons
            };
        }

        if (analysis.IsRangebound)
        {
            reasons.Insert(0, "Range-bound — 1H RSI(28) between 45–55 → Keltner fade");
            return new Signal
            {
                Instrument = instrument,
                Trend = TrendDirection.Neutral,
                Entry = $"{strike} straddle/IC",
                Strategy = "Keltner (20,1.5)/(20,2) fade + VWAP",
                StopLoss = "Beyond Keltner (20,2)",
                Target = "Mid / VWAP",
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
        var (entry, strategy, optionType) = bias == TrendDirection.Buy
            ? ($"{strike} CE", "Debit Spread", "CE")
            : ($"{strike} CE", "Sell ATM Call", "CE");

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
