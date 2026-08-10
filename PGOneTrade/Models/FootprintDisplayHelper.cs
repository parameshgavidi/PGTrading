namespace PGOneTrade.Models;

public static class FootprintDisplayHelper
{
    public static string FormatVolumeDelta(decimal delta)
    {
        var abs = Math.Abs(delta);
        if (abs >= 1_000_000m)
            return $"{abs / 1_000_000m:N1}M";

        if (abs >= 1_000m)
            return $"{abs / 1_000m:N1}K";

        return abs.ToString("N0");
    }

    public static TrendDirection GetFlowBias(FootprintAnalysis fp) => fp.PositiveDelta
        ? TrendDirection.Buy
        : fp.NegativeDelta
            ? TrendDirection.Sell
            : TrendDirection.Neutral;

    /// <summary>Futures/equity flow opposes the active trade or market bias.</summary>
    public static bool FootprintOpposesBias(FootprintAnalysis fp, TrendDirection bias)
    {
        if (bias == TrendDirection.Neutral)
            return false;

        var flow = GetFlowBias(fp);
        return flow != TrendDirection.Neutral && flow != bias;
    }

    public static string GetFlowBiasLabel(FootprintAnalysis fp) =>
        TrendUi.GetBiasLabel(GetFlowBias(fp));

    public static string GetInsightLabel(FootprintAnalysis fp)
    {
        if (fp.VolumeSource == "futures" && !string.IsNullOrEmpty(fp.FuturesSymbol))
        {
            var bias = GetFlowBiasLabel(fp);
            var deltaPart = fp.PositiveDelta
                ? $"buy > sell (Δ +{FormatVolumeDelta(fp.Delta)})"
                : fp.NegativeDelta
                    ? $"sell > buy (Δ −{FormatVolumeDelta(fp.Delta)})"
                    : $"balanced (Δ flat)";

            return $"{bias} fut flow · {deltaPart} · {fp.FuturesSymbol}";
        }

        if (fp.UsesVolumeProxy)
            return $"Footprint proxy · {fp.Summary}";

        return fp.Summary;
    }

    public static string GetShortDeltaLabel(FootprintAnalysis fp)
    {
        if (fp.PositiveDelta)
            return $"Δ +{FormatVolumeDelta(fp.Delta)}";

        if (fp.NegativeDelta)
            return $"Δ −{FormatVolumeDelta(fp.Delta)}";

        return "Δ flat";
    }

    /// <summary>
    /// Canonical footprint label for dashboard, AI panel, and framework logs.
    /// "Confirmed" only when framework step 4 passed (trade direction + footprint alignment).
    /// </summary>
    public static string GetDisplayLabel(FootprintAnalysis fp, bool footprintConfirmed)
    {
        if (fp.Summary is "Insufficient 5m data" or "No data")
            return fp.Summary;

        if (footprintConfirmed)
        {
            var symbolPart = fp.VolumeSource == "futures" && !string.IsNullOrEmpty(fp.FuturesSymbol)
                ? $" · {fp.FuturesSymbol}"
                : "";

            return $"Footprint OK · {GetFlowBiasLabel(fp)} · {GetShortDeltaLabel(fp)}{symbolPart}";
        }

        return GetInsightLabel(fp);
    }

    /// <summary>CSS class for footprint row — colors by order-flow bias, not framework pass/fail.</summary>
    public static string GetDisplayClass(FootprintAnalysis fp) =>
        TrendUi.GetClass(GetFlowBias(fp));

    /// <summary>Step 4 sub-checks for AI panel when directional bias is set.</summary>
    public static IReadOnlyList<(string Label, string State)> GetStep4Checks(FootprintAnalysis fp, TrendDirection bias)
    {
        if (fp.Summary is "Insufficient 5m data" or "No data")
            return [(fp.Summary, "fail")];

        if (bias == TrendDirection.Buy)
        {
            return [
                (GetDeltaCheckLabel(fp, TrendDirection.Buy), GetDeltaCheckState(fp, TrendDirection.Buy)),
                ("Stacked buy imbalances (≥3 × 5m)", fp.StackedBuyImbalance ? "pass" : "warn"),
                (fp.AbsorptionAgainstLong ? "Absorption vs long" : "No absorption vs long",
                    fp.AbsorptionAgainstLong ? "fail" : "pass")
            ];
        }

        if (bias == TrendDirection.Sell)
        {
            return [
                (GetDeltaCheckLabel(fp, TrendDirection.Sell), GetDeltaCheckState(fp, TrendDirection.Sell)),
                ("Stacked sell imbalances (≥3 × 5m)", fp.StackedSellImbalance ? "pass" : "warn"),
                (fp.AbsorptionAgainstShort ? "Absorption vs short" : "No absorption vs short",
                    fp.AbsorptionAgainstShort ? "fail" : "pass")
            ];
        }

        return [(GetInsightLabel(fp), "warn")];
    }

    /// <summary>One-line Step 4 breakdown for dashboard tables.</summary>
    public static string GetStep4BreakdownLine(FootprintAnalysis fp, TrendDirection bias, bool footprintConfirmed)
    {
        if (footprintConfirmed || bias == TrendDirection.Neutral)
            return string.Empty;

        if (fp.Summary is "Insufficient 5m data" or "No data")
            return fp.Summary;

        if (bias == TrendDirection.Buy)
        {
            var delta = fp.PositiveDelta ? "Δ ✓" : "Δ ✗";
            var stacked = fp.StackedBuyImbalance ? "stacked ✓" : "stacked ✗";
            var absorption = fp.AbsorptionAgainstLong ? "absorption ✗" : "no absorption ✓";
            return $"{delta} · {stacked} · {absorption}";
        }

        if (bias == TrendDirection.Sell)
        {
            var delta = fp.NegativeDelta ? "Δ ✓" : "Δ ✗";
            var stacked = fp.StackedSellImbalance ? "stacked ✓" : "stacked ✗";
            var absorption = fp.AbsorptionAgainstShort ? "absorption ✗" : "no absorption ✓";
            return $"{delta} · {stacked} · {absorption}";
        }

        return string.Empty;
    }

    /// <summary>Explicit WAIT detail when Step 4 is the blocker.</summary>
    public static string GetStep4BlockingDetail(FootprintAnalysis fp, TrendDirection bias)
    {
        if (fp.Summary is "Insufficient 5m data" or "No data")
            return $"Step 4 — {fp.Summary}.";

        if (bias == TrendDirection.Buy)
        {
            if (!fp.PositiveDelta)
                return "Step 4 — need positive 5m delta (net buying over last 8 bars on futures).";

            if (fp.AbsorptionAgainstLong)
                return "Step 4 — absorption against long on last 5m bar (heavy selling at highs).";

            if (!fp.StackedBuyImbalance)
                return "Step 4 — bullish delta OK but need ≥3 consecutive 5m bars with buy > sell × 1.4.";

            return "Step 4 — footprint not confirmed.";
        }

        if (bias == TrendDirection.Sell)
        {
            if (!fp.NegativeDelta)
                return "Step 4 — need negative 5m delta (net selling over last 8 bars on futures).";

            if (fp.AbsorptionAgainstShort)
                return "Step 4 — absorption against short on last 5m bar (heavy buying at lows).";

            if (!fp.StackedSellImbalance)
                return "Step 4 — bearish delta OK but need ≥3 consecutive 5m bars with sell > buy × 1.4.";

            return "Step 4 — footprint not confirmed.";
        }

        return "Step 4 — footprint not confirmed.";
    }

    private static string GetDeltaCheckLabel(FootprintAnalysis fp, TrendDirection bias)
    {
        if (bias == TrendDirection.Buy)
            return fp.PositiveDelta
                ? $"5m delta {GetShortDeltaLabel(fp)}"
                : "5m delta not positive";

        return fp.NegativeDelta
            ? $"5m delta {GetShortDeltaLabel(fp)}"
            : "5m delta not negative";
    }

    private static string GetDeltaCheckState(FootprintAnalysis fp, TrendDirection bias)
    {
        if (bias == TrendDirection.Buy)
            return fp.PositiveDelta ? "pass" : fp.NegativeDelta ? "fail" : "warn";

        return fp.NegativeDelta ? "pass" : fp.PositiveDelta ? "fail" : "warn";
    }
}
