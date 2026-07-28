namespace PGOne.Models;

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
}
