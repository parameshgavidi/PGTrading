using PGOne.Services;

namespace PGOne.Models;

/// <summary>Pre-flight checks before Auto Buy automation can place CNC orders.</summary>
public static class AutoBuyReadiness
{
    public sealed record Check(string Label, bool Passed, string Detail);

    public static IReadOnlyList<Check> Evaluate(
        bool masterAutomationEnabled,
        AutoBuyRow? row,
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

        if (row is null)
        {
            checks.Add(new("One NSE stock in list", false, "Add a symbol from the NSE equity search"));
            return checks;
        }

        checks.Add(new("One NSE stock in list", true, row.Symbol));
        checks.Add(new("Row automation", row.AutomationEnabled,
            row.AutomationEnabled ? "On" : "Off — enable per stock or was auto-disabled at max deploy"));
        checks.Add(new("NSE CNC delivery only", row.Exchange.Equals("NSE", StringComparison.OrdinalIgnoreCase),
            $"No MIS / F&O · product {AutoBuyDefaults.Product}"));

        if (row.MaxDeployAmount > 0)
        {
            var atMax = AutoBuyDeployHelper.IsMaxDeployReached(row.DeployedAmount, row.MaxDeployAmount);
            checks.Add(new("Below max deploy cap", !atMax,
                $"Deployed ₹{row.DeployedAmount:N0} · max ₹{row.MaxDeployAmount:N0}"));
        }
        else
        {
            checks.Add(new("Below max deploy cap", true, "No max set (₹0 = unlimited)"));
        }

        var triggerReady = row.Status is "Flip detected" or "Order placed" or "Ordered";
        checks.Add(new("ST(7,2.5) Sell→Buy flip", triggerReady,
            string.IsNullOrWhiteSpace(row.Detail) ? $"Watching on {row.Timeframe}" : row.Detail));

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
