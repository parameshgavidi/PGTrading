using System.ComponentModel;
using System.Runtime.CompilerServices;
using PgAiTrading.Models;
using PgAiTrading.Services;

namespace PgAiTrading.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly IZerodhaService _zerodha;
    private string _statusMessage = string.Empty;
    private bool _isConnecting;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings { get; set; } = new();
    public string StatusMessage => _statusMessage;
    public string RequestToken { get; set; } = string.Empty;
    public bool IsConnected => _zerodha.IsConnected;
    public bool IsConnecting => _isConnecting;

    public SettingsViewModel(ISettingsService settings, IZerodhaService zerodha)
    {
        _settings = settings;
        _zerodha = zerodha;
        _zerodha.ConnectionChanged += _ => Notify(nameof(IsConnected));
    }

    public async Task LoadAsync()
    {
        await _settings.LoadAsync();
        Settings = CloneSettings(_settings.Settings);
        Notify();
    }

    public async Task SaveAsync()
    {
        _settings.ApplySettings(Settings);
        await _settings.SaveSettingsAsync();
        _settings.ReloadFromStorage();
        Settings = CloneSettings(_settings.Settings);

        SetStatusMessage(_settings.Settings.AutoTradingEnabled
            ? "Settings saved! Auto Trading is ON — live orders allowed."
            : "Settings saved! Auto Trading is OFF — check the box above, then save again.");

        Notify(nameof(Settings));
    }

    public async Task ConnectAsync()
    {
        if (_isConnecting)
            return;

        if (string.IsNullOrWhiteSpace(RequestToken))
        {
            SetStatusMessage("Enter the request token from the Zerodha redirect URL.");
            return;
        }

        try
        {
            _isConnecting = true;
            Notify(nameof(IsConnecting));
            SetStatusMessage("Connecting to Zerodha…");

            _settings.ApplySettings(Settings);
            await _settings.SaveSettingsAsync();

            var (success, message) = await _zerodha.GenerateSessionAsync(RequestToken.Trim());
            SetStatusMessage(message);
            Notify(nameof(IsConnected));

            if (success)
                RequestToken = string.Empty;
        }
        finally
        {
            _isConnecting = false;
            Notify(nameof(IsConnecting));
        }
    }

    public void Disconnect()
    {
        _zerodha.Disconnect();
        SetStatusMessage("Disconnected from Zerodha.");
        Notify(nameof(IsConnected));
    }

    public void SetStatusMessage(string message)
    {
        _statusMessage = message;
        Notify(nameof(StatusMessage));
    }

    public bool TryGetLoginUrl(out string url)
    {
        Settings.ApiKey = (Settings.ApiKey ?? string.Empty).Trim();
        Settings.ApiSecret = (Settings.ApiSecret ?? string.Empty).Trim();
        _settings.ApplySettings(Settings);

        url = _zerodha.GetLoginUrl();
        if (!string.IsNullOrWhiteSpace(Settings.ApiKey)
            && !string.IsNullOrEmpty(url)
            && url.Contains("api_key=", StringComparison.Ordinal)
            && !url.EndsWith("api_key=", StringComparison.Ordinal))
            return true;

        SetStatusMessage("Please enter your API Key and save settings first.");
        return false;
    }

    private static AppSettings CloneSettings(AppSettings source) => new()
    {
        Broker = source.Broker,
        ApiKey = source.ApiKey,
        ApiSecret = source.ApiSecret,
        AccessToken = source.AccessToken,
        LotSize = source.LotSize,
        RiskPercent = source.RiskPercent,
        TargetProfitAmount = source.TargetProfitAmount,
        TargetLossAmount = source.TargetLossAmount,
        AutoTradingEnabled = source.AutoTradingEnabled,
        DesktopNotifications = source.DesktopNotifications,
        TelegramNotifications = source.TelegramNotifications,
        SoundNotifications = source.SoundNotifications,
        TelegramBotToken = source.TelegramBotToken,
        TelegramChatId = source.TelegramChatId,
        HuggingFaceApiToken = source.HuggingFaceApiToken,
        Theme = AppThemes.Normalize(source.Theme)
    };

    public async Task SetThemeAsync(string theme)
    {
        var normalized = AppThemes.Normalize(theme);
        Settings.Theme = normalized;
        _settings.Settings.Theme = normalized;
        await _settings.SaveSettingsAsync();
        Notify(nameof(Settings));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
