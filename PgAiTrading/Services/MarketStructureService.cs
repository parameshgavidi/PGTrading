using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IMarketStructureService
{
    MarketStructureAnalysis Analyze(IReadOnlyList<Candle> candles, int swingStrength = 2);
    MultiTimeframeStructure AnalyzeMulti(
        IReadOnlyList<Candle> candles1H,
        IReadOnlyList<Candle> candles15M,
        IReadOnlyList<Candle> candles5M);
}

/// <summary>
/// Fractal swing + HH/HL/LH/LL structure. 1H for direction, 15M for setup BOS, 5M for entry only.
/// </summary>
public sealed class MarketStructureService : IMarketStructureService
{
    public MultiTimeframeStructure AnalyzeMulti(
        IReadOnlyList<Candle> candles1H,
        IReadOnlyList<Candle> candles15M,
        IReadOnlyList<Candle> candles5M) =>
        new()
        {
            Structure1H = Analyze(candles1H, swingStrength: 2),
            Structure15M = Analyze(candles15M, swingStrength: 2),
            Structure5M = Analyze(candles5M, swingStrength: 2)
        };

    public MarketStructureAnalysis Analyze(IReadOnlyList<Candle> candles, int swingStrength = 2)
    {
        var result = new MarketStructureAnalysis();
        if (candles.Count < swingStrength * 2 + 3)
            return result;

        var swings = FindSwings(candles, swingStrength);
        result.Swings = swings;
        if (swings.Count < 2)
        {
            result.Summary = "Await swings";
            return result;
        }

        var highs = swings.Where(s => s.IsHigh).ToList();
        var lows = swings.Where(s => !s.IsHigh).ToList();

        if (highs.Count >= 2)
        {
            var prev = highs[^2].Price;
            var last = highs[^1].Price;
            result.HasHigherHigh = last > prev;
            result.HasLowerHigh = last < prev;
            result.LastSwingHigh = last;
        }
        else if (highs.Count == 1)
        {
            result.LastSwingHigh = highs[0].Price;
        }

        if (lows.Count >= 2)
        {
            var prev = lows[^2].Price;
            var last = lows[^1].Price;
            result.HasHigherLow = last > prev;
            result.HasLowerLow = last < prev;
            result.LastSwingLow = last;
        }
        else if (lows.Count == 1)
        {
            result.LastSwingLow = lows[0].Price;
        }

        result.Bias = ClassifyBias(result);
        DetectBos(candles, result);
        result.Summary = BuildSummary(result);
        return result;
    }

    private static StructureBias ClassifyBias(MarketStructureAnalysis s)
    {
        var bullish = s.HasHigherHigh && s.HasHigherLow;
        var bearish = s.HasLowerHigh && s.HasLowerLow;

        if (bullish && !bearish)
            return StructureBias.Bullish;
        if (bearish && !bullish)
            return StructureBias.Bearish;

        // Soft classification when only one leg confirms.
        if (s.HasHigherHigh && s.HasHigherLow)
            return StructureBias.Bullish;
        if (s.HasLowerHigh && s.HasLowerLow)
            return StructureBias.Bearish;
        if ((s.HasHigherHigh || s.HasHigherLow) && !(s.HasLowerHigh || s.HasLowerLow))
            return StructureBias.Bullish;
        if ((s.HasLowerHigh || s.HasLowerLow) && !(s.HasHigherHigh || s.HasHigherLow))
            return StructureBias.Bearish;

        if (s.LastSwingHigh is null && s.LastSwingLow is null)
            return StructureBias.Insufficient;

        return StructureBias.Mixed;
    }

    private static void DetectBos(IReadOnlyList<Candle> candles, MarketStructureAnalysis result)
    {
        if (candles.Count == 0)
            return;

        var close = candles[^1].Close;
        var priorClose = candles.Count > 1 ? candles[^2].Close : close;

        if (result.LastSwingHigh is decimal sh && close > sh && priorClose <= sh)
        {
            result.BosBullish = true;
            result.LatestEvent = result.Bias == StructureBias.Bearish
                ? StructureEvent.ChochBullish
                : StructureEvent.BosBullish;
        }
        else if (result.LastSwingLow is decimal sl && close < sl && priorClose >= sl)
        {
            result.BosBearish = true;
            result.LatestEvent = result.Bias == StructureBias.Bullish
                ? StructureEvent.ChochBearish
                : StructureEvent.BosBearish;
        }
        else if (result.LastSwingHigh is decimal sh2 && close > sh2)
        {
            result.BosBullish = true;
            result.LatestEvent = StructureEvent.BosBullish;
        }
        else if (result.LastSwingLow is decimal sl2 && close < sl2)
        {
            result.BosBearish = true;
            result.LatestEvent = StructureEvent.BosBearish;
        }
    }

    private static string BuildSummary(MarketStructureAnalysis s)
    {
        var legs = new List<string>();
        if (s.HasHigherHigh) legs.Add("HH");
        if (s.HasHigherLow) legs.Add("HL");
        if (s.HasLowerHigh) legs.Add("LH");
        if (s.HasLowerLow) legs.Add("LL");

        var bias = s.Bias switch
        {
            StructureBias.Bullish => "Bullish",
            StructureBias.Bearish => "Bearish",
            StructureBias.Mixed => "Mixed/chop",
            _ => "Insufficient"
        };

        var structure = legs.Count > 0 ? string.Join("+", legs) : "—";
        var evt = s.LatestEvent switch
        {
            StructureEvent.BosBullish => " · BOS↑",
            StructureEvent.BosBearish => " · BOS↓",
            StructureEvent.ChochBullish => " · CHOCH↑",
            StructureEvent.ChochBearish => " · CHOCH↓",
            _ => ""
        };

        return $"{bias} ({structure}){evt}";
    }

    private static List<SwingPoint> FindSwings(IReadOnlyList<Candle> candles, int strength)
    {
        var swings = new List<SwingPoint>();
        for (var i = strength; i < candles.Count - strength; i++)
        {
            var isHigh = true;
            var isLow = true;
            for (var j = 1; j <= strength; j++)
            {
                if (candles[i].High <= candles[i - j].High || candles[i].High <= candles[i + j].High)
                    isHigh = false;
                if (candles[i].Low >= candles[i - j].Low || candles[i].Low >= candles[i + j].Low)
                    isLow = false;
            }

            if (isHigh)
            {
                swings.Add(new SwingPoint
                {
                    Index = i,
                    Timestamp = candles[i].Timestamp,
                    Price = candles[i].High,
                    IsHigh = true
                });
            }
            else if (isLow)
            {
                swings.Add(new SwingPoint
                {
                    Index = i,
                    Timestamp = candles[i].Timestamp,
                    Price = candles[i].Low,
                    IsHigh = false
                });
            }
        }

        return swings;
    }
}
