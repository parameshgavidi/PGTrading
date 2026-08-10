using PgAiTrading.Services;

namespace PgAiTrading.Models;

/// <summary>Shared system gates before Auto Buy can place CNC orders (per-row ST signal is separate).</summary>
public static class AutoBuyReadiness
{
    public sealed record Check(string Label, bool Passed, string Detail);

    public static IReadOnlyList<Check> Evaluate(
        bool masterAutomationEnabled,
        IReadOnlyList<AutoBuyRow> rows,
        bool isConnected,
        bool autoTradingEnabled,
        bool isMarketOpen)
    {
        var checks = new List<Check>
        {
            new("Zerodha connected", isConnected, isConnected ? "Live broker session" : "Connect in Settings"),
            new("Market open (IST 9:15–15:30)", isMarketOpen,
                isMarketOpen ? "Session active" : "No CNC orders outside market hours"),
            new("Settings → Auto Trading", autoTradingEnabled,
                autoTradingEnabled ? "Enabled" : "Required for live order placement"),
            new("Master automation", masterAutomationEnabled,
                masterAutomationEnabled ? "On" : "Turn on master toggle on this page"),
        };

        if (rows.Count == 0)
        {
            checks.Add(new("NSE stocks in list", false, "Add symbols from the NSE equity search"));
            return checks;
        }

        var symbols = string.Join(", ", rows.Select(r => r.Symbol));
        checks.Add(new("NSE stocks in list", true, $"{rows.Count} — {symbols}"));

        var enabledRows = rows.Where(r => r.AutomationEnabled).ToList();
        var enabledCount = enabledRows.Count;
        checks.Add(new("Row automation", enabledCount > 0,
            enabledCount > 0
                ? $"{enabledCount} of {rows.Count} row(s) enabled"
                : "Enable automation on at least one row"));

        checks.Add(new("Long only — BUY CNC", true,
            $"Each row: ST(7,2.5) Buy trigger → {AutoBuyDefaults.EntrySide} only · never sell"));

        checks.Add(new("Per-stock entry", true,
            "Each enabled row places BUY when that stock's ST(7,2.5) turns Buy on its timeframe — other rows do not block it"));

        if (enabledCount == 0)
        {
            checks.Add(new("Deploy capacity", false, "Enable automation on at least one row"));
            return checks;
        }

        var atMax = enabledRows
            .Where(r => r.MaxDeployAmount > 0
                && AutoBuyDeployHelper.IsMaxDeployReached(r.DeployedAmount, r.MaxDeployAmount))
            .Select(r => r.Symbol)
            .ToList();

        var canStillEnter = enabledRows.Count(r =>
            r.MaxDeployAmount <= 0
            || !AutoBuyDeployHelper.IsMaxDeployReached(r.DeployedAmount, r.MaxDeployAmount));

        var cappedCount = enabledRows.Count(r => r.MaxDeployAmount > 0);

        if (canStillEnter == 0 && cappedCount > 0)
        {
            checks.Add(new("Deploy capacity", false,
                $"All enabled rows at max deploy — {string.Join(", ", atMax)}"));
        }
        else if (atMax.Count > 0)
        {
            checks.Add(new("Deploy capacity", true,
                $"{canStillEnter} enabled row(s) can still enter · at cap: {string.Join(", ", atMax)}"));
        }
        else
        {
            checks.Add(new("Deploy capacity", true,
                cappedCount > 0
                    ? $"{cappedCount} row(s) with per-stock caps — room available"
                    : "No per-stock caps set"));
        }

        return checks;
    }

    public static bool CanPlaceOrder(
        AutoBuyRow row,
        bool isConnected,
        bool autoTradingEnabled,
        bool isMarketOpen,
        int quantity,
        decimal limitPrice)
    {
        if (!isConnected || !autoTradingEnabled || !isMarketOpen)
            return false;

        if (!row.AutomationEnabled)
            return false;

        if (!row.Exchange.Equals("NSE", StringComparison.OrdinalIgnoreCase))
            return false;

        if (quantity < 1 || limitPrice <= 0)
            return false;

        var orderValue = quantity * limitPrice;

        if (AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount))
            return false;

        if (AutoBuyDeployHelper.WouldExceedMax(row.DeployedAmount, row.MaxDeployAmount, orderValue))
            return false;

        return true;
    }
}
