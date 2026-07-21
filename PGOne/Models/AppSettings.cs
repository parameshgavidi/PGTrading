namespace PGOne.Models;

public class AppSettings
{
    public string Broker { get; set; } = "Zerodha";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public int LotSize { get; set; } = 1;
    public decimal RiskPercent { get; set; } = 2.0m;
    public bool AutoTradingEnabled { get; set; }
    public bool DesktopNotifications { get; set; } = true;
    public bool TelegramNotifications { get; set; }
    public bool SoundNotifications { get; set; } = true;
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
    /// <summary>Optional free token from huggingface.co/settings/tokens for FinBERT rate limits.</summary>
    public string HuggingFaceApiToken { get; set; } = string.Empty;
}
