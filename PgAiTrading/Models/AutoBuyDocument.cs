namespace PgAiTrading.Models;

/// <summary>
/// Versioned Auto Buy document — portable across desktop file storage and future web APIs.
/// Runtime-only fields (Status, Detail, DeployedAmount) are not persisted.
/// </summary>
public sealed class AutoBuyDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public bool MasterAutomationEnabled { get; set; }
    public List<AutoBuyPersistedRow> Rows { get; set; } = new();

    public static AutoBuyDocument FromRuntime(bool masterAutomationEnabled, IEnumerable<AutoBuyRow> rows) =>
        new()
        {
            Version = CurrentVersion,
            MasterAutomationEnabled = masterAutomationEnabled,
            Rows = rows.Select(AutoBuyPersistedRow.FromRuntime).ToList()
        };

    public (bool Master, List<AutoBuyRow> Rows) ToRuntime() =>
        (MasterAutomationEnabled, Rows.Select(r => r.ToRuntime()).ToList());
}

/// <summary>Persisted Auto Buy row fields shared by local JSON and future per-user APIs.</summary>
public sealed class AutoBuyPersistedRow
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public string Timeframe { get; set; } = "5m";
    public int Lots { get; set; } = 1;
    public decimal MaxDeployAmount { get; set; }
    public bool AutomationEnabled { get; set; }

    public static AutoBuyPersistedRow FromRuntime(AutoBuyRow row) =>
        new()
        {
            Symbol = row.Symbol,
            Exchange = row.Exchange,
            Timeframe = AutoBuyTimeframes.Normalize(row.Timeframe),
            Lots = Math.Max(1, row.Lots),
            MaxDeployAmount = Math.Max(0, row.MaxDeployAmount),
            AutomationEnabled = row.AutomationEnabled
        };

    public AutoBuyRow ToRuntime() =>
        new()
        {
            Symbol = (Symbol ?? string.Empty).Trim().ToUpperInvariant(),
            Exchange = string.IsNullOrWhiteSpace(Exchange) ? "NSE" : Exchange.Trim().ToUpperInvariant(),
            Timeframe = AutoBuyTimeframes.Normalize(Timeframe),
            Lots = Math.Max(1, Lots),
            MaxDeployAmount = Math.Max(0, MaxDeployAmount),
            AutomationEnabled = AutomationEnabled
        };
}
