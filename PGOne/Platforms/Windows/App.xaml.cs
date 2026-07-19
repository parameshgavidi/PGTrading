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

        // Some machines (VMs, remote desktop sessions, certain GPU drivers) render
        // WebView2 content as a solid black rectangle due to GPU compositing issues,
        // even though the page loads successfully. Disabling GPU acceleration for the
        // WebView2 browser process forces software rendering, which fixes this.
        Environment.SetEnvironmentVariable(
            "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
            "--disable-gpu --disable-gpu-compositing --disable-gpu-driver-bug-workarounds");

        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
