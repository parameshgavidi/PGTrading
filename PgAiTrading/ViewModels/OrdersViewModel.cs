using System.ComponentModel;
using System.Runtime.CompilerServices;
using PgAiTrading.Models;
using PgAiTrading.Models.Ui;
using PgAiTrading.Services;

namespace PgAiTrading.ViewModels;

public class OrdersViewModel : INotifyPropertyChanged
{
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<Order> Orders { get; private set; } = Array.Empty<Order>();
    public bool IsConnected { get; private set; }
    public bool IsLoading { get; private set; }

    public OrdersViewModel(IZerodhaService zerodha)
    {
        _zerodha = zerodha;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Notify(nameof(IsLoading));

        IsConnected = _zerodha.IsConnected;
        Orders = IsConnected
            ? await _zerodha.GetOrdersAsync()
            : Array.Empty<Order>();

        IsLoading = false;
        Notify(nameof(IsConnected));
        Notify(nameof(Orders));
        Notify(nameof(IsLoading));
    }

    public string OfflineMessage => BrokerUiMessages.BrokerOfflineOrders;

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
