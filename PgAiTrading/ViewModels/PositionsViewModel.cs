using System.ComponentModel;
using System.Runtime.CompilerServices;
using PgAiTrading.Models;
using PgAiTrading.Models.Ui;
using PgAiTrading.Services;

namespace PgAiTrading.ViewModels;

public class PositionsViewModel : INotifyPropertyChanged
{
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<Position> Positions { get; private set; } = Array.Empty<Position>();
    public bool IsConnected { get; private set; }
    public bool IsLoading { get; private set; }

    public PositionsViewModel(IZerodhaService zerodha)
    {
        _zerodha = zerodha;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Notify(nameof(IsLoading));

        IsConnected = _zerodha.IsConnected;
        Positions = IsConnected
            ? await _zerodha.GetPositionsAsync()
            : Array.Empty<Position>();

        IsLoading = false;
        Notify(nameof(IsConnected));
        Notify(nameof(Positions));
        Notify(nameof(IsLoading));
    }

    public string OfflineMessage => BrokerUiMessages.BrokerOfflinePositions;

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
