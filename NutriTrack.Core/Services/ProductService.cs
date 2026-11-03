using System.Globalization;

namespace NutriTrack.Core.Services;

/// <summary>Loads the shared tab-separated product catalogue => Blank nutrition fields mean unknown, never zero.</summary>
public class ProductService
{
    public List<Models.Product> LoadAllowedProducts(string filePath)
    {
        var products = new List<Models.Product>();
        if (!File.Exists(filePath)) return products;

        using var reader = new StreamReader(filePath);
        string? header = reader.ReadLine();
        if (header == null) return products;
        char delimiter = header.Contains('\t') ? '\t' : ',';

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(delimiter);
            if (parts.Length < 12 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;

            products.Add(new Models.Product
            {
                ProductId = id, Name = parts[1].Trim(), Category = EmptyToNull(parts[2]),
                Brand = EmptyToNull(parts[3]), Unit = string.IsNullOrWhiteSpace(parts[4]) ? "g" : parts[4].Trim(),
                CaloriesPer100 = Number(parts[5]), ProteinPer100 = Number(parts[6]),
                CarbsPer100 = Number(parts[7]), FatPer100 = Number(parts[8]),
                FiberPer100 = Number(parts[9]), SugarPer100 = Number(parts[10]), SaltPer100 = Number(parts[11])
            });
        }
        return products;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
}
