namespace PGOneTrade.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        // WebView2 env vars are set in WebView2Bootstrap.cs via [ModuleInitializer]
        // so they apply before the WebView2 native DLL is loaded.
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
