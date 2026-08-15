using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgAiTrading.Models;

/// <summary>Read/write versioned <see cref="LongTermScanDocument"/> JSON files.</summary>
public static class LongTermScanJsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static LongTermScanDocument? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var doc = JsonSerializer.Deserialize<LongTermScanDocument>(json, Options);
            if (doc is null)
                return null;

            if (doc.Version <= 0)
                doc.Version = LongTermScanDocument.CurrentVersion;

            doc.Items ??= new List<StockScanRow>();
            return doc;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(string path, LongTermScanDocument document)
    {
        document.Version = document.Version <= 0
            ? LongTermScanDocument.CurrentVersion
            : document.Version;
        document.Items ??= new List<StockScanRow>();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(path, json);
    }
}
