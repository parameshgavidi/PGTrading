using System.Globalization;
using System.Text;

namespace PgAiTrading.Models;

/// <summary>Persists Auto Buy master toggle and watchlist rows to a single CSV file.</summary>
public static class AutoBuyCsvFile
{
    public const string MasterPrefix = "MASTER";
    public const string Header = "Symbol,Exchange,Timeframe,Lots,AutomationEnabled,MaxDeployAmount";

    public static (bool MasterAutomationEnabled, List<AutoBuyRow> Rows) Load(string path)
    {
        if (!File.Exists(path))
            return (false, new List<AutoBuyRow>());

        var masterEnabled = false;
        var rows = new List<AutoBuyRow>();
        var seenHeader = false;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var parts = ParseCsvLine(line);
            if (parts.Count == 0)
                continue;

            if (parts[0].Equals(MasterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                masterEnabled = ParseBool(parts.Count > 1 ? parts[1] : "false");
                continue;
            }

            if (!seenHeader && parts[0].Equals("Symbol", StringComparison.OrdinalIgnoreCase))
            {
                seenHeader = true;
                continue;
            }

            if (parts.Count < 5)
                continue;

            var symbol = parts[0].Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(symbol))
                continue;

            rows.Add(new AutoBuyRow
            {
                Symbol = symbol,
                Exchange = parts[1].Trim().ToUpperInvariant(),
                Timeframe = NormalizeTimeframe(parts[2]),
                Lots = ParseInt(parts[3], 1),
                AutomationEnabled = ParseBool(parts[4]),
                MaxDeployAmount = parts.Count > 5 ? ParseDecimal(parts[5]) : 0m
            });
        }

        return (masterEnabled, rows);
    }

    public static void Save(string path, bool masterAutomationEnabled, IReadOnlyList<AutoBuyRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{MasterPrefix},{masterAutomationEnabled.ToString().ToLowerInvariant()}");
        sb.AppendLine(Header);

        foreach (var row in rows.OrderBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(string.Join(",",
                Escape(row.Symbol),
                Escape(row.Exchange),
                Escape(NormalizeTimeframe(row.Timeframe)),
                Math.Max(1, row.Lots).ToString(CultureInfo.InvariantCulture),
                row.AutomationEnabled.ToString().ToLowerInvariant(),
                row.MaxDeployAmount.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    public static string NormalizeTimeframe(string? timeframe) =>
        AutoBuyTimeframes.IsValid(timeframe) ? timeframe! : "5m";

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : fallback;

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0m;

    private static bool ParseBool(string value) =>
        bool.TryParse(value.Trim(), out var b) && b
        || value.Trim() is "1" or "yes" or "Y" or "TRUE";

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                    inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }

        parts.Add(current.ToString());
        return parts;
    }
}
