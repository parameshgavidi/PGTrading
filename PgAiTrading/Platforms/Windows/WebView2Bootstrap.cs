using System.Runtime.CompilerServices;

namespace PgAiTrading.WinUI;

/// <summary>
/// Runs before any WebView2 native code loads so environment variables take effect.
/// Setting these in App.xaml.cs is often too late — WebView2 reads them at process start.
/// </summary>
internal static class WebView2Bootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PgAiTrading",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

        // Software rendering avoids the solid-black WebView2 rectangle on VMs,
        // Remote Desktop, and some GPU drivers.
        Environment.SetEnvironmentVariable(
            "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
            "--disable-gpu --disable-gpu-compositing --disable-gpu-driver-bug-workarounds");
    }
}
