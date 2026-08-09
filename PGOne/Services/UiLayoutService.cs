using Microsoft.JSInterop;

namespace PGOne.Services;

public interface IUiLayoutService
{
    bool IsNavCollapsed { get; }
    bool IsWatchlistCollapsed { get; }
    bool IsFocusChart { get; }
    event Action? Changed;

    Task InitializeAsync();
    Task ToggleNavAsync();
    Task SetNavCollapsedAsync(bool collapsed);
    Task ToggleWatchlistAsync();
    Task SetWatchlistCollapsedAsync(bool collapsed);
    Task ToggleFocusChartAsync();
}

/// <summary>
/// Shared layout preferences for chart-first UX: nav collapse, watchlist collapse, focus mode.
/// Persisted in localStorage so the desk feels the same on relaunch.
/// </summary>
public sealed class UiLayoutService : IUiLayoutService
{
    private const string NavKey = "pgone.layout.navCollapsed";
    private const string WatchKey = "pgone.layout.watchlistCollapsed";
    private const string FocusKey = "pgone.layout.focusChart";

    private readonly IJSRuntime _js;
    private bool _ready;
    private bool _navBeforeFocus;
    private bool _watchBeforeFocus;

    public bool IsNavCollapsed { get; private set; }
    public bool IsWatchlistCollapsed { get; private set; }
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
            IsWatchlistCollapsed = await GetBoolAsync(WatchKey);
            IsFocusChart = await GetBoolAsync(FocusKey);

            // Focus implies both collapsed; heal inconsistent storage.
            if (IsFocusChart)
            {
                IsNavCollapsed = true;
                IsWatchlistCollapsed = true;
            }
        }
        catch
        {
            // JS not ready / unavailable — keep defaults (all open).
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

    public Task ToggleWatchlistAsync() => SetWatchlistCollapsedAsync(!IsWatchlistCollapsed);

    public async Task SetWatchlistCollapsedAsync(bool collapsed)
    {
        if (IsWatchlistCollapsed == collapsed && _ready)
            return;

        IsWatchlistCollapsed = collapsed;
        if (!collapsed && IsFocusChart)
            IsFocusChart = false;

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
        }
        else
        {
            _navBeforeFocus = IsNavCollapsed;
            _watchBeforeFocus = IsWatchlistCollapsed;
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
            await SetBoolAsync(FocusKey, IsFocusChart);
        }
        catch
        {
            // Ignore persistence failures (still update in-memory UI).
        }
    }

    private async Task<bool> GetBoolAsync(string key)
    {
        var value = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private Task SetBoolAsync(string key, bool value) =>
        _js.InvokeVoidAsync("localStorage.setItem", key, value ? "1" : "0").AsTask();
}
