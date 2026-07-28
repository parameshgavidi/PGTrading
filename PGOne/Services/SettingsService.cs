using PGOne.Models;

namespace PGOne.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    StrategyConfig Strategy { get; }
    LongTermStrategyConfig LongTermStrategy { get; }
    void ApplySettings(AppSettings settings);
    Task SaveSettingsAsync();
    Task SaveStrategyAsync();
    Task SaveLongTermStrategyAsync();
    Task LoadAsync();
}

public class SettingsService : ISettingsService
{
    private const string SettingsKey = "pgone_settings";
    private const string StrategyKey = "pgone_strategy";
    private const string LongTermStrategyKey = "pgone_strategy_longterm";

    public AppSettings Settings { get; private set; } = new();
    public StrategyConfig Strategy { get; private set; } = new();
    public LongTermStrategyConfig LongTermStrategy { get; private set; } = new();

    public async Task LoadAsync()
    {
        var settingsJson = Preferences.Default.Get(SettingsKey, string.Empty);
        if (!string.IsNullOrEmpty(settingsJson))
            Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(settingsJson) ?? new();

        var strategyJson = Preferences.Default.Get(StrategyKey, string.Empty);
        if (!string.IsNullOrEmpty(strategyJson))
            Strategy = System.Text.Json.JsonSerializer.Deserialize<StrategyConfig>(strategyJson) ?? new();

        var longTermJson = Preferences.Default.Get(LongTermStrategyKey, string.Empty);
        if (!string.IsNullOrEmpty(longTermJson))
            LongTermStrategy = System.Text.Json.JsonSerializer.Deserialize<LongTermStrategyConfig>(longTermJson) ?? new();

        await Task.CompletedTask;
    }

    public void ApplySettings(AppSettings settings)
    {
        Settings.Broker = settings.Broker;
        Settings.ApiKey = settings.ApiKey;
        Settings.ApiSecret = settings.ApiSecret;
        Settings.AccessToken = settings.AccessToken;
        Settings.LotSize = settings.LotSize;
        Settings.RiskPercent = settings.RiskPercent;
        Settings.TargetProfitAmount = settings.TargetProfitAmount;
        Settings.TargetLossAmount = settings.TargetLossAmount;
        Settings.AutoTradingEnabled = settings.AutoTradingEnabled;
        Settings.DesktopNotifications = settings.DesktopNotifications;
        Settings.TelegramNotifications = settings.TelegramNotifications;
        Settings.SoundNotifications = settings.SoundNotifications;
        Settings.TelegramBotToken = settings.TelegramBotToken;
        Settings.TelegramChatId = settings.TelegramChatId;
        Settings.HuggingFaceApiToken = settings.HuggingFaceApiToken;
    }

    public async Task SaveSettingsAsync()
    {
        Preferences.Default.Set(SettingsKey, System.Text.Json.JsonSerializer.Serialize(Settings));
        await Task.CompletedTask;
    }

    public async Task SaveStrategyAsync()
    {
        Preferences.Default.Set(StrategyKey, System.Text.Json.JsonSerializer.Serialize(Strategy));
        await Task.CompletedTask;
    }

    public async Task SaveLongTermStrategyAsync()
    {
        Preferences.Default.Set(LongTermStrategyKey, System.Text.Json.JsonSerializer.Serialize(LongTermStrategy));
        await Task.CompletedTask;
    }
}
