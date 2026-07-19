using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings { get; private set; } = new();
    public string StatusMessage { get; private set; } = string.Empty;
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
        StatusMessage = "Settings saved!";
        Notify(nameof(StatusMessage));
    }

    public string GetLoginUrl() => _zerodha.GetLoginUrl();

    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(RequestToken))
        {
            StatusMessage = "Enter the request token from Zerodha login redirect URL.";
            Notify(nameof(StatusMessage));
            return;
        }

        var success = await _zerodha.GenerateSessionAsync(RequestToken.Trim());
        StatusMessage = success
            ? $"Connected as {_zerodha.UserId}"
            : "Connection failed. Check API Key, Secret, and Request Token.";
        Notify(nameof(StatusMessage));
        Notify(nameof(IsConnected));
    }

    public void Disconnect()
    {
        _zerodha.Disconnect();
        StatusMessage = "Disconnected from Zerodha.";
        Notify(nameof(StatusMessage));
        Notify(nameof(IsConnected));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
