using Microsoft.JSInterop;
using PgAiTrading.Models;

namespace PgAiTrading.Services;

public interface IThemeService
{
    string CurrentTheme { get; }
    event Action? ThemeChanged;
    Task InitializeAsync();
    Task ApplyAsync(string theme);
}

/// <summary>
/// Applies the persisted UI theme to the document (CSS data-theme) and charts.
/// Appearance only — no trading behavior.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IJSRuntime _js;
    private readonly ISettingsService _settings;
    private bool _ready;

    public string CurrentTheme { get; private set; } = AppThemes.Black;
    public event Action? ThemeChanged;

    public ThemeService(IJSRuntime js, ISettingsService settings)
    {
        _js = js;
        _settings = settings;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public async Task InitializeAsync()
    {
        if (_ready)
            return;

        await _settings.LoadAsync();
        await ApplyAsync(_settings.Settings.Theme);
        _ready = true;
    }

    public async Task ApplyAsync(string theme)
    {
        var normalized = AppThemes.Normalize(theme);
        CurrentTheme = normalized;

        try
        {
            await _js.InvokeVoidAsync("pgAiTradingTheme.apply", normalized);
        }
        catch
        {
            // JS may not be ready on first paint; CSS defaults to black.
        }

        ThemeChanged?.Invoke();
    }

    private void OnSettingsChanged()
    {
        var next = AppThemes.Normalize(_settings.Settings.Theme);
        if (string.Equals(next, CurrentTheme, StringComparison.OrdinalIgnoreCase))
            return;

        _ = ApplyAsync(next);
    }
}
