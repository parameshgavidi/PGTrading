using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using PGOne.Services;
using PGOne.ViewModels;

namespace PGOne;

public static class MauiProgram
{
    static MauiProgram()
    {
        // Fixes BlazorWebView rendering as a blank/empty rectangle on Windows.
        // Newer WebView2/WinAppSDK builds changed the internal host address used
        // to serve Blazor content; this restores the working 0.0.0.0 behavior.
        // https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview
        AppContext.SetSwitch("BlazorWebView.AppHostAddressAlways0000", true);
    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        ConfigureWebView2();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IZerodhaService, ZerodhaService>();
        builder.Services.AddSingleton<IMarketDataService, MarketDataService>();
        builder.Services.AddSingleton<ISuperTrendService, SuperTrendService>();
        builder.Services.AddSingleton<IIndicatorService, IndicatorService>();
        builder.Services.AddSingleton<IFootprintService, FootprintService>();
        builder.Services.AddSingleton<IVolumeProfileService, VolumeProfileService>();
        builder.Services.AddSingleton<ISignalService, SignalService>();
        builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
        builder.Services.AddSingleton<IIntradayScannerService, IntradayScannerService>();
        builder.Services.AddSingleton<ILongTermScannerService, LongTermScannerService>();
        builder.Services.AddSingleton<IFundamentalDataService, FundamentalDataService>();
        builder.Services.AddSingleton<ILongTermFrameworkService, LongTermFrameworkService>();
        builder.Services.AddSingleton<IHoldingsService, HoldingsService>();
        builder.Services.AddSingleton<ITrailingStopLossService, TrailingStopLossService>();
        builder.Services.AddSingleton<ILongTermExitMonitorService, LongTermExitMonitorService>();
        builder.Services.AddSingleton<IStrategyService, StrategyService>();
        builder.Services.AddSingleton<ISentimentService, SentimentService>();
        builder.Services.AddSingleton<IIntradayCprService, IntradayCprService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<StrategyViewModel>();
        builder.Services.AddSingleton<SignalViewModel>();
        builder.Services.AddSingleton<WatchlistViewModel>();
        builder.Services.AddSingleton<HoldingsViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SentimentViewModel>();
        builder.Services.AddSingleton<Cpr1mViewModel>();

        return builder.Build();
    }

    private static void ConfigureWebView2()
    {
        // UserDataFolder and GPU flags are set via ModuleInitializer in
        // Platforms/Windows/WebView2Bootstrap.cs (must run before WebView2 loads).
        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("PGOneWebView2", (handler, _) =>
        {
            if (handler.PlatformView is not WebView2 webView)
                return;

            webView.CoreWebView2Initialized += (_, args) =>
            {
                if (args.Exception is null)
                {
#if DEBUG
                    try
                    {
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                    }
                    catch
                    {
                        // DevTools are optional; ignore if unavailable.
                    }
#endif
                    return;
                }

                var message = args.Exception.Message
                    ?? "WebView2 failed to initialize. Install the WebView2 Runtime (see install-webview2.ps1).";

                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {message}");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Application.Current?.Windows.FirstOrDefault()?.Page is MainPage mainPage)
                        mainPage.ShowWebViewError(message);
                });
            };
        });
    }
}
