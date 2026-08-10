using System.Text.Json;
using PgAiTrading.Models;

namespace PgAiTrading.Services;

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
    private const string SettingsKey = "pgaitrading_settings";
    private const string StrategyKey = "pgaitrading_strategy";
    private const string LongTermStrategyKey = "pgaitrading_strategy_longterm";

    // Pre-rename keys (PGOne) — read once to restore broker settings after rebrand.
    private const string LegacySettingsKey = "pgone_settings";
    private const string LegacyStrategyKey = "pgone_strategy";
    private const string LegacyLongTermStrategyKey = "pgone_strategy_longterm";

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
        MigrateLegacyPreferencesIfNeeded();
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

    /// <summary>
    /// After PGOne → PgAiTrading rename, preference keys changed. Copy old values
    /// into the new keys once so Zerodha API key/secret/token are not lost.
    /// </summary>
    private static void MigrateLegacyPreferencesIfNeeded()
    {
        TryMigrateKey(LegacySettingsKey, SettingsKey);
        TryMigrateKey(LegacyStrategyKey, StrategyKey);
        TryMigrateKey(LegacyLongTermStrategyKey, LongTermStrategyKey);
    }

    private static void TryMigrateKey(string legacyKey, string newKey)
    {
        var existing = Preferences.Default.Get(newKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
            return;

        var legacy = Preferences.Default.Get(legacyKey, string.Empty);
        if (string.IsNullOrEmpty(legacy))
            return;

        Preferences.Default.Set(newKey, legacy);
    }

    public void ApplySettings(AppSettings settings)
    {
        Settings.Broker = settings.Broker;
        Settings.ApiKey = (settings.ApiKey ?? string.Empty).Trim();
        Settings.ApiSecret = (settings.ApiSecret ?? string.Empty).Trim();
        Settings.AccessToken = (settings.AccessToken ?? string.Empty).Trim();
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
