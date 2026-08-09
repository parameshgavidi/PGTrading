namespace PGOne.Models;

public class AppSettings
{
    public string Broker { get; set; } = "Zerodha";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public int LotSize { get; set; } = 1;
    public decimal RiskPercent { get; set; } = 2.0m;
    /// <summary>Aggregate open-position P&L target (₹). Exit all when sum &gt;= this value.</summary>
    public decimal TargetProfitAmount { get; set; }
    /// <summary>Aggregate open-position loss limit (₹). Exit all when sum &lt;= −this value.</summary>
    public decimal TargetLossAmount { get; set; }
    public bool AutoTradingEnabled { get; set; }
    public bool DesktopNotifications { get; set; } = true;
    public bool TelegramNotifications { get; set; }
    public bool SoundNotifications { get; set; } = true;
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
    /// <summary>Required free token from huggingface.co/settings/tokens for FinBERT sentiment analysis.</summary>
    public string HuggingFaceApiToken { get; set; } = string.Empty;

    /// <summary>
    /// UI color theme. Use <see cref="AppThemes.Black"/> or <see cref="AppThemes.Classic"/>
    /// (White &amp; Blue). Does not affect trading or broker logic.
    /// </summary>
    public string Theme { get; set; } = AppThemes.Black;
}
