using Microsoft.UI.Xaml;

namespace PGOne.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        // WebView2 must write cache to a user-writable folder (required for unpackaged apps).
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PGOne",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
