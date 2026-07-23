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

    public SignalService(
        IMarketDataService marketData,
        ISuperTrendService superTrend,
        IIndicatorService indicators,
        ISettingsService settings)
    {
        _marketData = marketData;
        _superTrend = superTrend;
        _indicators = indicators;
        _settings = settings;
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

        // Framework: 1H directional bias from RSI(28).
        var rsiTrend = _indicators.CalculateRsi(candles1H, config.RsiTrendLength);
        var rsiBias = rsiTrend > config.RsiBullThreshold ? TrendDirection.Buy
            : rsiTrend < config.RsiBearThreshold ? TrendDirection.Sell
            : TrendDirection.Neutral;

        // ADX(14) on 1H → trend strength band.
        var adx = _indicators.CalculateAdx(candles1H, config.AdxLength);
        var strength1H = adx < config.AdxWeakThreshold ? TrendStrength.Weak
            : adx < config.AdxStrongThreshold ? TrendStrength.Moderate
            : TrendStrength.Strong;

        // RSI(14) shown in the panel + per-timeframe RSI for the reversal guard.
        var rsi = _indicators.CalculateRsi(candles1H, config.RsiLength);
        var rsi5M = _indicators.CalculateRsi(candles5M, config.RsiLength);
        var rsi15M = _indicators.CalculateRsi(candles15M, config.RsiLength);

        // Reversal guard: any timeframe RSI below the reversal threshold.
        string? reversalReason = null;
        if (rsi5M < config.RsiReversalThreshold) reversalReason = $"5m RSI {rsi5M:0} < {config.RsiReversalThreshold:0}";
        else if (rsi15M < config.RsiReversalThreshold) reversalReason = $"15m RSI {rsi15M:0} < {config.RsiReversalThreshold:0}";
        else if (rsiTrend < config.RsiReversalThreshold) reversalReason = $"1H RSI {rsiTrend:0} < {config.RsiReversalThreshold:0}";

        // 5m VWAP context.
        var vwap5M = candles5M.LastOrDefault(c => c.Vwap.HasValue)?.Vwap ?? 0m;
        var last5MClose = candles5M.Count > 0 ? candles5M[^1].Close : 0m;
        var aboveVwap = vwap5M > 0 && last5MClose >= vwap5M;

        var cpr = _indicators.GetCprBias(candlesDay.Count >= 2 ? candlesDay : candles1H);

        var isRangebound = rsiBias == TrendDirection.Neutral;
        var score = CalculateScore(rsiBias, trend15M, trend5M, strength1H, aboveVwap, isRangebound);

        return new MultiTimeframeAnalysis
        {
            Trend1H = trend1H,
            Trend15M = trend15M,
            Trend5M = trend5M,
            Rsi = rsi,
            RsiTrend = rsiTrend,
            RsiBias = rsiBias,
            Adx = adx,
            Strength1H = strength1H,
            Cpr = cpr,
            Vwap5M = vwap5M,
            AboveVwap = aboveVwap,
            IsRangebound = isRangebound,
            WaitForReversal = reversalReason is not null,
            ReversalReason = reversalReason,
            Rsi5M = rsi5M,
            Rsi15M = rsi15M,
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
        var stopLoss = BuildStopLoss(instrument, analysis.RsiBias, candles5M);

        var reasons = new List<string>
        {
            $"1H RSI({_settings.Strategy.RsiTrendLength}) {analysis.RsiTrend:0} → {BiasLabel(analysis.RsiBias)}",
            $"1H ADX {analysis.Adx:0} → {analysis.Strength1H} trend",
            $"5m {(analysis.AboveVwap ? "above" : "below")} VWAP {analysis.Vwap5M:N0}",
            $"CPR {analysis.Cpr}"
        };

        // 1) Reversal guard — RSI < 30 on any timeframe: stand aside.
        if (analysis.WaitForReversal)
        {
            reasons.Insert(0, $"⚠ Possible reversal: {analysis.ReversalReason}");
            return NoTrade(instrument, "Wait — possible reversal", analysis.OverallScore, reasons);
        }

        // 2) Range-bound (1H RSI 45–55): mean-reversion with Keltner Channels on 5m.
        if (analysis.IsRangebound)
        {
            reasons.Insert(0, "Range-bound 1H → Keltner mean-reversion on 5m");
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

        // 3) Trending (1H RSI bias) — align 5m SuperTrend and VWAP with the bias.
        var bias = analysis.RsiBias;
        var stAligned = analysis.Trend5M == bias || analysis.Trend15M == bias;
        var vwapAligned = bias == TrendDirection.Buy ? analysis.AboveVwap : !analysis.AboveVwap;
        var strongEnough = analysis.Strength1H != TrendStrength.Weak;

        if (!stAligned || !vwapAligned || !strongEnough)
        {
            if (!strongEnough) reasons.Add("✖ ADX weak (<18) — avoid trend trades");
            if (!stAligned) reasons.Add("✖ 5m/15m SuperTrend not aligned with 1H bias");
            if (!vwapAligned) reasons.Add("✖ 5m VWAP not aligned with bias");
            return NoTrade(instrument, "Wait for alignment", analysis.OverallScore, reasons);
        }

        var (entry, strategy, optionType) = bias == TrendDirection.Buy
            ? ($"{strike} CE", "Debit Spread", "CE")
            : ($"{strike} CE", "Sell ATM Call", "CE");

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
            Target = "Risk : Reward 1 : 2",
            Confidence = analysis.OverallScore,
            Reasons = reasons
        };
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

    private static int CalculateScore(TrendDirection bias, TrendDirection t15, TrendDirection t5, TrendStrength strength, bool aboveVwap, bool isRangebound)
    {
        if (isRangebound)
            return 45;

        var score = 40;

        if (t15 == bias) score += 15;
        if (t5 == bias) score += 15;

        score += strength switch
        {
            TrendStrength.Strong => 20,
            TrendStrength.Moderate => 10,
            _ => 0
        };

        var vwapAligned = bias == TrendDirection.Buy ? aboveVwap : !aboveVwap;
        if (vwapAligned) score += 10;

        return Math.Clamp(score, 0, 99);
    }

    private static string MapInstrument(string instrument) => InstrumentMapper.ToZerodhaKey(instrument);
}
