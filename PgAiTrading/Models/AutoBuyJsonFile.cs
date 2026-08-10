using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgAiTrading.Models;

/// <summary>Read/write versioned <see cref="AutoBuyDocument"/> JSON files.</summary>
public static class AutoBuyJsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AutoBuyDocument? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var doc = JsonSerializer.Deserialize<AutoBuyDocument>(json, Options);
            if (doc is null)
                return null;

            if (doc.Version <= 0)
                doc.Version = AutoBuyDocument.CurrentVersion;

            doc.Rows ??= new List<AutoBuyPersistedRow>();
            return doc;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(string path, AutoBuyDocument document)
    {
        document.Version = document.Version <= 0 ? AutoBuyDocument.CurrentVersion : document.Version;
        document.Rows ??= new List<AutoBuyPersistedRow>();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(path, json);
    }
}
