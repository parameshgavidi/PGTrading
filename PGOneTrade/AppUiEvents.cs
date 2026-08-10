namespace PGOneTrade;

/// <summary>
/// Signals when the Blazor shell has rendered so the native loading banner can hide.
/// </summary>
public static class AppUiEvents
{
    public static event Action? ShellRendered;

    public static void RaiseShellRendered() => ShellRendered?.Invoke();
}
