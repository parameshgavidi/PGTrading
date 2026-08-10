using System.Text.Json;
using PGOneTrade.Models;

namespace PGOneTrade.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    StrategyConfig Strategy { get; }
    LongTermStrategyConfig LongTermStrategy { get; }
    event Action? SettingsChanged;
    void ApplySettings(AppSettings settings);
    void ReloadFromStorage();
    Task SaveSettingsAsync();
    Task SaveStrategyAsync();
    Task SaveLongTermStrategyAsync();
    Task LoadAsync();
}

public class SettingsService : ISettingsService
{
    private const string SettingsKey = "pgonetrade_settings";
    private const string StrategyKey = "pgonetrade_strategy";
    private const string LongTermStrategyKey = "pgonetrade_strategy_longterm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AppSettings Settings { get; private set; } = new();
    public StrategyConfig Strategy { get; private set; } = new();
    public LongTermStrategyConfig LongTermStrategy { get; private set; } = new();

    public event Action? SettingsChanged;

    public async Task LoadAsync()
    {
        ReloadFromStorage();

        var strategyJson = Preferences.Default.Get(StrategyKey, string.Empty);
        if (!string.IsNullOrEmpty(strategyJson))
            Strategy = JsonSerializer.Deserialize<StrategyConfig>(strategyJson, JsonOptions) ?? new();

        var longTermJson = Preferences.Default.Get(LongTermStrategyKey, string.Empty);
        if (!string.IsNullOrEmpty(longTermJson))
            LongTermStrategy = JsonSerializer.Deserialize<LongTermStrategyConfig>(longTermJson, JsonOptions) ?? new();

        await Task.CompletedTask;
    }

    public void ReloadFromStorage()
    {
        var settingsJson = Preferences.Default.Get(SettingsKey, string.Empty);
        if (string.IsNullOrEmpty(settingsJson))
            return;

        var loaded = JsonSerializer.Deserialize<AppSettings>(settingsJson, JsonOptions);
        if (loaded is null)
            return;

        ApplySettings(loaded);
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
        Settings.Theme = AppThemes.Normalize(settings.Theme);
    }

    public async Task SaveSettingsAsync()
    {
        Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(Settings, JsonOptions));
        SettingsChanged?.Invoke();
        await Task.CompletedTask;
    }

    public async Task SaveStrategyAsync()
    {
        Preferences.Default.Set(StrategyKey, JsonSerializer.Serialize(Strategy, JsonOptions));
        await Task.CompletedTask;
    }

    public async Task SaveLongTermStrategyAsync()
    {
        Preferences.Default.Set(LongTermStrategyKey, JsonSerializer.Serialize(LongTermStrategy, JsonOptions));
        await Task.CompletedTask;
    }
}
