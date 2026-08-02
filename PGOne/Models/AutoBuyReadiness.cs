using PGOne.Services;

namespace PGOne.Models;

/// <summary>Pre-flight checks before Auto Buy automation can place CNC orders.</summary>
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

        var enabledCount = rows.Count(r => r.AutomationEnabled);
        checks.Add(new("Row automation", enabledCount > 0,
            enabledCount > 0
                ? $"{enabledCount} of {rows.Count} row(s) enabled"
                : "Enable automation on at least one row"));

        checks.Add(new("Long only — BUY CNC", true,
            $"Each row: ST(7,2.5) Buy trigger → {AutoBuyDefaults.EntrySide} only · never sell"));

        var atMax = rows
            .Where(r => r.MaxDeployAmount > 0
                && AutoBuyDeployHelper.IsMaxDeployReached(r.DeployedAmount, r.MaxDeployAmount))
            .Select(r => r.Symbol)
            .ToList();

        if (atMax.Count > 0)
            checks.Add(new("Below per-stock max deploy", false,
                $"{string.Join(", ", atMax)} at or above cap"));
        else
        {
            var capped = rows.Count(r => r.MaxDeployAmount > 0);
            checks.Add(new("Below per-stock max deploy", true,
                capped > 0 ? $"{capped} row(s) with per-stock caps — all within limit" : "No per-stock caps set"));
        }

        var signalRows = rows
            .Where(r => r.Status is "Buy signal" or "Order placed" or "Ordered")
            .Select(r => $"{r.Symbol} ({r.Timeframe})")
            .ToList();

        checks.Add(new("ST(7,2.5) buy signal", signalRows.Count > 0,
            signalRows.Count > 0
                ? string.Join(", ", signalRows)
                : "Waiting for ST to turn Buy on each row's timeframe"));

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
