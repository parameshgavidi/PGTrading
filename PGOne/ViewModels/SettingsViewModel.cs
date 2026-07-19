using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly IZerodhaService _zerodha;
    private string _statusMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings { get; set; } = new();
    public string StatusMessage => _statusMessage;
    public string RequestToken { get; set; } = string.Empty;
    public bool IsConnected => _zerodha.IsConnected;

    public SettingsViewModel(ISettingsService settings, IZerodhaService zerodha)
    {
        _settings = settings;
        _zerodha = zerodha;
        _zerodha.ConnectionChanged += _ => Notify(nameof(IsConnected));
    }

    public async Task LoadAsync()
    {
        await _settings.LoadAsync();
        Settings = _settings.Settings;
        Notify();
    }

    public async Task SaveAsync()
    {
        await _settings.SaveSettingsAsync();
        SetStatusMessage("Settings saved!");
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(RequestToken))
        {
            SetStatusMessage("Enter the request token from Zerodha login redirect URL.");
            return;
        }

        var success = await _zerodha.GenerateSessionAsync(RequestToken.Trim());
        SetStatusMessage(success
            ? $"Connected as {_zerodha.UserId}"
            : "Connection failed. Check API Key, Secret, and Request Token.");
        Notify(nameof(IsConnected));
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
        url = _zerodha.GetLoginUrl();
        if (!string.IsNullOrEmpty(url) && url.Contains("api_key=") && !url.EndsWith("api_key="))
            return true;

        SetStatusMessage("Please enter your API Key and save settings first.");
        return false;
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
