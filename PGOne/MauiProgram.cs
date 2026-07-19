using Microsoft.Extensions.Logging;
using PGOne.Services;
using PGOne.ViewModels;

namespace PGOne;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Fixes BlazorWebView rendering as a blank/empty rectangle on Windows.
        // Newer WebView2/WinAppSDK builds broke the default 0.0.0.0 host address
        // used to serve Blazor content; this restores the working behavior.
        // https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview
        AppContext.SetSwitch("BlazorWebView.AppHostAddressAlways0000", true);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

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
}
