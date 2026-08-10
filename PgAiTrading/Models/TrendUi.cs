namespace PgAiTrading.Models;

public static class TrendUi
{
    public static string GetClass(TrendDirection trend) => trend switch
    {
        TrendDirection.Buy => "buy",
        TrendDirection.Sell => "sell",
        _ => "neutral"
    };

    public static string GetBadgeLabel(TrendDirection trend) => trend switch
    {
        TrendDirection.Buy => "🟢 BUY",
        TrendDirection.Sell => "🔴 SELL",
        _ => "⚪ NEUTRAL"
    };

    public static string GetIcon(TrendDirection trend) => trend switch
    {
        TrendDirection.Buy => "🟢",
        TrendDirection.Sell => "🔴",
        _ => "⚪"
    };

    public static string GetBiasLabel(TrendDirection bias) => bias switch
    {
        TrendDirection.Buy => "Bullish",
        TrendDirection.Sell => "Bearish",
        _ => "Neutral"
    };

    public static string GetSuperTrendLabel(TrendDirection trend) => trend switch
    {
        TrendDirection.Buy => "Bullish",
        TrendDirection.Sell => "Bearish",
        _ => "Neutral"
    };
}
