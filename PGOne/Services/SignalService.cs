using PGOne.Models;

namespace PGOne.Services;

public interface ISignalService
{
    Task<Signal> GenerateSignalAsync(string instrument = "NIFTY");
    Task<MultiTimeframeAnalysis> AnalyzeAsync(string instrument = "NIFTY");
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
    {
        var symbol = MapInstrument(instrument);
        var config = _settings.Strategy;

        var candles1H = await _marketData.GetCandlesAsync(symbol, "1H", 100);
        var candles15M = await _marketData.GetCandlesAsync(symbol, "15m", 100);
        var candles5M = await _marketData.GetCandlesAsync(symbol, "5m", 100);

        var trend1H = _superTrend.GetTrend(candles1H, config.SuperTrend1HPeriod, config.SuperTrend1HMultiplier);
        var trend15M = _superTrend.GetTrend(candles15M, config.SuperTrend15MPeriod, config.SuperTrend15MMultiplier);
        var trend5M = _superTrend.GetTrend(candles5M, config.SuperTrend5MPeriod, config.SuperTrend5MMultiplier);

        var rsi = _indicators.CalculateRsi(candles5M, config.RsiLength);
        var adx = _indicators.CalculateAdx(candles5M, config.AdxLength);
        var cpr = _indicators.GetCprBias(candles5M);

        var score = CalculateScore(trend1H, trend15M, trend5M, rsi, adx, cpr, config);

        return new MultiTimeframeAnalysis
        {
            Trend1H = trend1H,
            Trend15M = trend15M,
            Trend5M = trend5M,
            Rsi = rsi,
            Adx = adx,
            Cpr = cpr,
            OverallScore = score,
            Strength = score >= 85 ? "Strong" : score >= 60 ? "Moderate" : "Weak"
        };
    }

    public async Task<Signal> GenerateSignalAsync(string instrument = "NIFTY")
    {
        var analysis = await AnalyzeAsync(instrument);
        var config = _settings.Strategy;
        var price = await _marketData.GetCurrentPriceAsync(MapInstrument(instrument));
        var strike = Math.Round(price / 50) * 50;

        var aligned = analysis.Trend1H == analysis.Trend15M && analysis.Trend15M == analysis.Trend5M;
        var trend = analysis.Trend5M;
        var rsiOk = trend == TrendDirection.Buy ? analysis.Rsi > 55 : analysis.Rsi < 45;
        var adxOk = analysis.Adx > config.MinimumAdx;

        var reasons = new List<string>();
        if (analysis.Trend1H != TrendDirection.Neutral)
            reasons.Add($"✔ 1H SuperTrend {analysis.Trend1H}");
        if (analysis.Trend15M != TrendDirection.Neutral)
            reasons.Add($"✔ 15m SuperTrend {analysis.Trend15M}");
        if (analysis.Trend5M != TrendDirection.Neutral)
            reasons.Add($"✔ 5m Pullback Complete");
        reasons.Add($"✔ RSI {(int)analysis.Rsi}");
        reasons.Add($"✔ ADX {(int)analysis.Adx}");
        reasons.Add($"✔ CPR {analysis.Cpr}");

        var confidence = analysis.OverallScore;
        string entry, strategy;

        if (!aligned || !rsiOk || !adxOk || trend == TrendDirection.Neutral)
        {
            return new Signal
            {
                Instrument = instrument,
                Trend = TrendDirection.Neutral,
                Entry = "No Trade",
                Strategy = "Wait for alignment",
                StopLoss = "-",
                Target = "-",
                Confidence = confidence,
                Reasons = reasons
            };
        }

        if (trend == TrendDirection.Buy)
        {
            entry = $"{strike:F0} CE";
            strategy = "Debit Spread";
        }
        else
        {
            entry = $"{strike:F0} PE";
            strategy = "Credit Spread";
        }

        return new Signal
        {
            Instrument = instrument,
            Trend = trend,
            Entry = entry,
            Strategy = strategy,
            StopLoss = "Spread x2",
            Target = "Risk : Reward 1 : 2",
            Confidence = confidence,
            Reasons = reasons
        };
    }

    private static int CalculateScore(TrendDirection t1, TrendDirection t2, TrendDirection t3, decimal rsi, decimal adx, string cpr, StrategyConfig config)
    {
        var score = 50;

        if (t1 == t2 && t2 == t3 && t1 != TrendDirection.Neutral) score += 25;
        else if (t1 == t2 || t2 == t3) score += 10;

        if (adx > config.MinimumAdx) score += 10;
        if (adx > config.MinimumAdx + 5) score += 5;

        if (t1 == TrendDirection.Buy && rsi > 55) score += 5;
        if (t1 == TrendDirection.Sell && rsi < 45) score += 5;

        if (cpr == "Bullish" && t1 == TrendDirection.Buy) score += 5;
        if (cpr == "Bearish" && t1 == TrendDirection.Sell) score += 5;

        return Math.Min(score, 99);
    }

    private static string MapInstrument(string instrument) => instrument.ToUpper() switch
    {
        "NIFTY" => "NSE:NIFTY 50",
        "BANKNIFTY" => "NSE:NIFTY BANK",
        "RELIANCE" => "NSE:RELIANCE",
        "INFY" => "NSE:INFY",
        "TCS" => "NSE:TCS",
        "SBIN" => "NSE:SBIN",
        "HDFCBANK" => "NSE:HDFCBANK",
        _ => $"NSE:{instrument}"
    };
}
