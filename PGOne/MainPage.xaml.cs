using Microsoft.AspNetCore.Components.WebView.Maui;

namespace PGOne;

public partial class MainPage : ContentPage
{
    private bool _uiLoaded;

    public MainPage()
    {
        InitializeComponent();
        AppUiEvents.ShellRendered += OnShellRendered;
        StartLoadWatchdog();
    }

    private void OnShellRendered()
    {
        MainThread.BeginInvokeOnMainThread(NotifyBlazorUiLoaded);
    }

    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        if (e.Exception is not null)
        {
            ShowWebViewError(
                "Blazor WebView failed to start: " + e.Exception.Message +
                "\n\nRun .\\verify-build.ps1 and .\\clean.ps1 then rebuild (F5).");
            return;
        }

        statusBanner.Text = "Loading PG One UI…";
        errorPanel.IsVisible = false;
    }

    public void NotifyBlazorUiLoaded()
    {
        _uiLoaded = true;
        statusBanner.IsVisible = false;
        errorPanel.IsVisible = false;
    }

    public void ShowWebViewError(string message)
    {
        statusBanner.IsVisible = false;
        errorMessage.Text = message;
        errorPanel.IsVisible = true;
    }

    private void StartLoadWatchdog()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_uiLoaded || errorPanel.IsVisible)
                    return;

                ShowWebViewError(
                    "The trading UI did not load within 30 seconds. " +
                    "Common fixes: run .\\sync-maui-version.ps1 then rebuild; " +
                    "install WebView2 Runtime (install-webview2.ps1); " +
                    "run .\\clean.ps1 and press F5 again. " +
                    "In DEBUG builds, check the WebView2 DevTools console for errors.");
            });
        });
    }
}
