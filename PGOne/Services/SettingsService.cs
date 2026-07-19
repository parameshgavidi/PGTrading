using PGOne.Models;

namespace PGOne.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    StrategyConfig Strategy { get; }
    Task SaveSettingsAsync();
    Task SaveStrategyAsync();
    Task LoadAsync();
}

public class SettingsService : ISettingsService
{
    private const string SettingsKey = "pgone_settings";
    private const string StrategyKey = "pgone_strategy";

    public AppSettings Settings { get; private set; } = new();
    public StrategyConfig Strategy { get; private set; } = new();

    public async Task LoadAsync()
    {
        var settingsJson = Preferences.Default.Get(SettingsKey, string.Empty);
        if (!string.IsNullOrEmpty(settingsJson))
            Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(settingsJson) ?? new();

        var strategyJson = Preferences.Default.Get(StrategyKey, string.Empty);
        if (!string.IsNullOrEmpty(strategyJson))
            Strategy = System.Text.Json.JsonSerializer.Deserialize<StrategyConfig>(strategyJson) ?? new();

        await Task.CompletedTask;
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
}
