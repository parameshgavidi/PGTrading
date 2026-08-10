namespace PGOneTrade.Models.Trading;

/// <summary>UI and broker product codes.</summary>
public static class ProductTypes
{
    public const string Mis = "MIS";
    public const string Cnc = "CNC";
    public const string Nrml = "NRML";

    public static bool IsUiProduct(string? product) =>
        string.Equals(product, Mis, StringComparison.OrdinalIgnoreCase)
        || string.Equals(product, Cnc, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeUi(string product)
    {
        if (string.Equals(product, Mis, StringComparison.OrdinalIgnoreCase))
            return Mis;
        if (string.Equals(product, Cnc, StringComparison.OrdinalIgnoreCase))
            return Cnc;

        throw new ArgumentOutOfRangeException(nameof(product), product, "UI product must be MIS or CNC.");
    }
}
