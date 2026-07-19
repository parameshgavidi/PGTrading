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
        builder.Services.AddSingleton<ISignalService, SignalService>();
        builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
        builder.Services.AddSingleton<IStrategyService, StrategyService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<StrategyViewModel>();
        builder.Services.AddSingleton<SignalViewModel>();
        builder.Services.AddSingleton<WatchlistViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        return builder.Build();
    }

    private static void ConfigureWebView2()
    {
        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("PGOneWebView2", (handler, _) =>
        {
            if (handler.PlatformView is not WebView2 webView)
                return;

            webView.CoreWebView2InitializationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                    return;

                var message = args.InitializationException?.Message
                    ?? "WebView2 failed to initialize. Install the WebView2 Runtime and restart.";

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
