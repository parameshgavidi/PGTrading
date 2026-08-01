using System.ComponentModel;
using System.Runtime.CompilerServices;
using PGOne.Models;
using PGOne.Services;

namespace PGOne.ViewModels;

public class AutoBuyViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAutoBuyService _autoBuy;
    private readonly IZerodhaService _zerodha;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AutoBuyViewModel(IAutoBuyService autoBuy, IZerodhaService zerodha)
    {
        _autoBuy = autoBuy;
        _zerodha = zerodha;
        _autoBuy.Updated += OnAutoBuyUpdated;
    }

    public bool MasterAutomationEnabled => _autoBuy.MasterAutomationEnabled;
    public IReadOnlyList<AutoBuyRow> Rows => _autoBuy.Rows;
    public IReadOnlyList<string> NseSymbols => _autoBuy.NseSymbols;
    public bool IsLoadingSymbols => _autoBuy.IsLoadingSymbols;
    public bool IsMonitoring => _autoBuy.IsMonitoring;
    public string? StatusMessage => _autoBuy.StatusMessage;
    public string CsvPath => _autoBuy.CsvPath;
    public bool IsConnected => _zerodha.IsConnected;

    public async Task InitializeAsync() => await _autoBuy.InitializeAsync();

    public async Task RefreshSymbolsAsync() => await _autoBuy.RefreshSymbolsAsync();

    public IReadOnlyList<string> SearchSymbols(string query) => _autoBuy.SearchSymbols(query);

    public async Task AddSymbolAsync(string symbol) => await _autoBuy.AddSymbolAsync(symbol);

    public async Task RemoveSymbolAsync(string symbol) => await _autoBuy.RemoveSymbolAsync(symbol);

    public async Task UpdateRowAsync(AutoBuyRow row) => await _autoBuy.UpdateRowAsync(row);

    public async Task SetRowAutomationAsync(string symbol, bool enabled) =>
        await _autoBuy.SetRowAutomationAsync(symbol, enabled);

    public async Task SetMasterAutomationAsync(bool enabled) =>
        await _autoBuy.SetMasterAutomationAsync(enabled);

    public async Task RefreshDeployedAmountsAsync() =>
        await _autoBuy.RefreshDeployedAmountsAsync();

    private void OnAutoBuyUpdated()
    {
        Notify(nameof(MasterAutomationEnabled));
        Notify(nameof(Rows));
        Notify(nameof(IsMonitoring));
        Notify(nameof(StatusMessage));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose() => _autoBuy.Updated -= OnAutoBuyUpdated;
}
