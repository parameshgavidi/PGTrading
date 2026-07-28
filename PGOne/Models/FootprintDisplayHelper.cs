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

    public static string GetInsightLabel(FootprintAnalysis fp)
    {
        if (fp.VolumeSource == "futures" && !string.IsNullOrEmpty(fp.FuturesSymbol))
        {
            var deltaPart = fp.PositiveDelta
                ? $"buy vol > sell (Δ +{FormatVolumeDelta(fp.Delta)})"
                : fp.NegativeDelta
                    ? $"sell vol > buy (Δ −{FormatVolumeDelta(fp.Delta)})"
                    : "buy/sell balanced (Δ flat)";

            return $"Fut footprint · {deltaPart} · {fp.FuturesSymbol}";
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
}
