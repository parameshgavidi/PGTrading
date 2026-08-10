using Microsoft.JSInterop;

namespace PgAiTrading.Services;

public interface IUiLayoutService
{
    bool IsNavCollapsed { get; }
    bool IsWatchlistCollapsed { get; }
    bool IsNiftyAccordionOpen { get; }
    bool IsFocusChart { get; }
    event Action? Changed;

    Task InitializeAsync();
    Task ToggleNavAsync();
    Task SetNavCollapsedAsync(bool collapsed);
    Task ToggleWatchlistAsync();
    Task SetWatchlistCollapsedAsync(bool collapsed);
    Task ToggleNiftyAccordionAsync();
    Task SetNiftyAccordionOpenAsync(bool open);
    Task ToggleFocusChartAsync();
}

/// <summary>
/// Shared layout preferences for chart-first UX: nav collapse, Nifty accordion, focus mode.
/// Persisted in localStorage so the desk feels the same on relaunch.
/// </summary>
public sealed class UiLayoutService : IUiLayoutService
{
    private const string NavKey = "pgaitrading.layout.navCollapsed";
    private const string WatchKey = "pgaitrading.layout.watchlistCollapsed";
    private const string NiftyAccKey = "pgaitrading.layout.niftyAccordionOpen";
    private const string FocusKey = "pgaitrading.layout.focusChart";

    private readonly IJSRuntime _js;
    private bool _ready;
    private bool _navBeforeFocus;
    private bool _watchBeforeFocus;

    public bool IsNavCollapsed { get; private set; }
    public bool IsWatchlistCollapsed { get; private set; } = true; // side panel retired; keep collapsed
    public bool IsNiftyAccordionOpen { get; private set; } = true;
    public bool IsFocusChart { get; private set; }
    public event Action? Changed;

    public UiLayoutService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_ready)
            return;

        try
        {
            IsNavCollapsed = await GetBoolAsync(NavKey);
            IsWatchlistCollapsed = true; // always use nav accordion instead of side panel
            IsNiftyAccordionOpen = await GetBoolAsync(NiftyAccKey, defaultValue: true);
            IsFocusChart = await GetBoolAsync(FocusKey);

            if (IsFocusChart)
                IsNavCollapsed = true;
        }
        catch
        {
            // JS not ready / unavailable — keep defaults.
        }

        _ready = true;
        Changed?.Invoke();
    }

    public Task ToggleNavAsync() => SetNavCollapsedAsync(!IsNavCollapsed);

    public async Task SetNavCollapsedAsync(bool collapsed)
    {
        if (IsNavCollapsed == collapsed && _ready)
            return;

        IsNavCollapsed = collapsed;
        if (!collapsed && IsFocusChart)
            IsFocusChart = false;

        await PersistAsync();
        Changed?.Invoke();
    }

    public Task ToggleWatchlistAsync() => ToggleNiftyAccordionAsync();

    public async Task SetWatchlistCollapsedAsync(bool collapsed)
    {
        // Legacy API: "collapsed" maps to accordion closed.
        await SetNiftyAccordionOpenAsync(!collapsed);
    }

    public Task ToggleNiftyAccordionAsync() => SetNiftyAccordionOpenAsync(!IsNiftyAccordionOpen);

    public async Task SetNiftyAccordionOpenAsync(bool open)
    {
        if (IsNiftyAccordionOpen == open && _ready && !IsNavCollapsed)
            return;

        IsNiftyAccordionOpen = open;
        IsWatchlistCollapsed = !open;

        // Opening Nifty stocks should reveal the nav if it was hidden.
        if (open && IsNavCollapsed)
        {
            IsNavCollapsed = false;
            if (IsFocusChart)
                IsFocusChart = false;
        }

        await PersistAsync();
        Changed?.Invoke();
    }

    public async Task ToggleFocusChartAsync()
    {
        if (IsFocusChart)
        {
            IsFocusChart = false;
            IsNavCollapsed = _navBeforeFocus;
            IsWatchlistCollapsed = _watchBeforeFocus;
            IsNiftyAccordionOpen = !_watchBeforeFocus;
        }
        else
        {
            _navBeforeFocus = IsNavCollapsed;
            _watchBeforeFocus = !IsNiftyAccordionOpen;
            IsFocusChart = true;
            IsNavCollapsed = true;
            IsWatchlistCollapsed = true;
        }

        await PersistAsync();
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            await SetBoolAsync(NavKey, IsNavCollapsed);
            await SetBoolAsync(WatchKey, IsWatchlistCollapsed);
            await SetBoolAsync(NiftyAccKey, IsNiftyAccordionOpen);
            await SetBoolAsync(FocusKey, IsFocusChart);
        }
        catch
        {
            // Ignore persistence failures (still update in-memory UI).
        }
    }

    private async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var value = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private Task SetBoolAsync(string key, bool value) =>
        _js.InvokeVoidAsync("localStorage.setItem", key, value ? "1" : "0").AsTask();
}
