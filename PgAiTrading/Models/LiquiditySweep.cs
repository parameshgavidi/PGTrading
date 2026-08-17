namespace PgAiTrading.Models;

public enum LiquiditySweepSide
{
    None,
    SellSide, // swept a low (PDL / VAL / swing low) — potential bullish reclaim
    BuySide   // swept a high (PDH / VAH / swing high) — potential bearish reclaim
}

/// <summary>
/// Liquidity sweep is a price-action event at a volume-profile / session reference level.
/// Entry requires: sweep → rejection → reclaim → 5M BOS, with footprint absorption preferred.
/// </summary>
public sealed class LiquiditySweepAnalysis
{
    public bool Detected { get; set; }
    public LiquiditySweepSide Side { get; set; } = LiquiditySweepSide.None;
    public string? LevelName { get; set; }
    public decimal LevelPrice { get; set; }
    public decimal SweepExtreme { get; set; }
    public bool Reclaimed { get; set; }
    public bool FootprintAbsorbed { get; set; }
    public bool StructureConfirmed { get; set; }
    public string Summary { get; set; } = "No sweep";

    public TrendDirection ImpliedDirection => Side switch
    {
        LiquiditySweepSide.SellSide when Reclaimed => TrendDirection.Buy,
        LiquiditySweepSide.BuySide when Reclaimed => TrendDirection.Sell,
        _ => TrendDirection.Neutral
    };

    /// <summary>High-quality setup: sweep + reclaim + 5M BOS (+ footprint absorption when available).</summary>
    public bool IsConfirmedSetup =>
        Detected && Reclaimed && StructureConfirmed;

    public bool Confirms(TrendDirection direction) =>
        IsConfirmedSetup && ImpliedDirection == direction;
}

public static class LiquiditySweepEvaluator
{
    private const decimal LevelTolerancePct = 0.0008m; // ~0.08%

    public static LiquiditySweepAnalysis Evaluate(
        IReadOnlyList<Candle> candles5M,
        VolumeProfileLevels profile,
        MarketStructureAnalysis structure15M,
        MarketStructureAnalysis structure5M,
        FootprintAnalysis footprint,
        int lookbackBars = 12)
    {
        var result = new LiquiditySweepAnalysis();
        if (candles5M.Count < 4)
            return result;

        var levels = BuildReferenceLevels(profile, structure15M);
        if (levels.Count == 0)
        {
            result.Summary = "Await reference levels";
            return result;
        }

        var start = Math.Max(0, candles5M.Count - lookbackBars);
        LiquiditySweepAnalysis? best = null;

        for (var i = start; i < candles5M.Count; i++)
        {
            var bar = candles5M[i];
            foreach (var (name, price, side) in levels)
            {
                var candidate = TryDetectSweep(candles5M, i, name, price, side);
                if (candidate is null)
                    continue;

                if (best is null
                    || candidate.Reclaimed && !best.Reclaimed
                    || (candidate.Reclaimed == best.Reclaimed
                        && Math.Abs(candidate.SweepExtreme - candidate.LevelPrice)
                           > Math.Abs(best.SweepExtreme - best.LevelPrice)))
                {
                    best = candidate;
                }
            }
        }

        if (best is null)
            return result;

        best.FootprintAbsorbed = best.Side switch
        {
            LiquiditySweepSide.SellSide => footprint.AbsorptionAgainstShort || footprint.PositiveDelta,
            LiquiditySweepSide.BuySide => footprint.AbsorptionAgainstLong || footprint.NegativeDelta,
            _ => false
        };

        best.StructureConfirmed = best.Side switch
        {
            LiquiditySweepSide.SellSide => structure5M.BosBullish
                || structure5M.LatestEvent is StructureEvent.BosBullish or StructureEvent.ChochBullish,
            LiquiditySweepSide.BuySide => structure5M.BosBearish
                || structure5M.LatestEvent is StructureEvent.BosBearish or StructureEvent.ChochBearish,
            _ => false
        };

        best.Summary = BuildSummary(best);
        return best;
    }

    private static LiquiditySweepAnalysis? TryDetectSweep(
        IReadOnlyList<Candle> candles,
        int barIndex,
        string levelName,
        decimal level,
        LiquiditySweepSide side)
    {
        var bar = candles[barIndex];
        var tol = Math.Max(level * LevelTolerancePct, 0.5m);

        if (side == LiquiditySweepSide.SellSide)
        {
            // Price takes liquidity below the level.
            if (bar.Low > level - tol * 0.25m || bar.Low >= level)
                return null;

            // Prefer a wick through rather than a strong close far below without reclaim attempt.
            var swept = bar.Low < level;
            if (!swept)
                return null;

            var reclaimed = false;
            for (var j = barIndex; j < candles.Count; j++)
            {
                if (candles[j].Close > level)
                {
                    reclaimed = true;
                    break;
                }
            }

            return new LiquiditySweepAnalysis
            {
                Detected = true,
                Side = LiquiditySweepSide.SellSide,
                LevelName = levelName,
                LevelPrice = level,
                SweepExtreme = bar.Low,
                Reclaimed = reclaimed
            };
        }

        if (side == LiquiditySweepSide.BuySide)
        {
            if (bar.High < level + tol * 0.25m || bar.High <= level)
                return null;

            if (bar.High <= level)
                return null;

            var reclaimed = false;
            for (var j = barIndex; j < candles.Count; j++)
            {
                if (candles[j].Close < level)
                {
                    reclaimed = true;
                    break;
                }
            }

            return new LiquiditySweepAnalysis
            {
                Detected = true,
                Side = LiquiditySweepSide.BuySide,
                LevelName = levelName,
                LevelPrice = level,
                SweepExtreme = bar.High,
                Reclaimed = reclaimed
            };
        }

        return null;
    }

    private static List<(string Name, decimal Price, LiquiditySweepSide Side)> BuildReferenceLevels(
        VolumeProfileLevels profile,
        MarketStructureAnalysis structure15M)
    {
        var levels = new List<(string, decimal, LiquiditySweepSide)>();

        void AddLow(string name, decimal price)
        {
            if (price > 0)
                levels.Add((name, price, LiquiditySweepSide.SellSide));
        }

        void AddHigh(string name, decimal price)
        {
            if (price > 0)
                levels.Add((name, price, LiquiditySweepSide.BuySide));
        }

        AddLow("PDL", profile.Pdl);
        AddHigh("PDH", profile.Pdh);
        AddLow("VAL", profile.Val);
        AddHigh("VAH", profile.Vah);
        AddLow("POC", profile.Poc);
        AddHigh("POC", profile.Poc);
        AddLow("Prev VAL", profile.PrevDayVal);
        AddHigh("Prev VAH", profile.PrevDayVah);

        if (structure15M.LastSwingLow is decimal swingLow)
            AddLow("15M swing low", swingLow);
        if (structure15M.LastSwingHigh is decimal swingHigh)
            AddHigh("15M swing high", swingHigh);

        return levels;
    }

    private static string BuildSummary(LiquiditySweepAnalysis s)
    {
        if (!s.Detected)
            return "No sweep";

        var side = s.Side == LiquiditySweepSide.SellSide ? "sell-side" : "buy-side";
        var reclaim = s.Reclaimed ? "reclaimed" : "not reclaimed";
        var bos = s.StructureConfirmed ? " · 5M BOS" : "";
        var abs = s.FootprintAbsorbed ? " · absorbed" : "";
        return $"{side} sweep of {s.LevelName} {s.LevelPrice:N0} → {reclaim}{abs}{bos}";
    }
}
