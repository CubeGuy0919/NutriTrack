using System.Globalization;

namespace NutriTrack.Core.Services;

/// <summary>Reads the product list (Products.txt). Lines that cannot be read are skipped.</summary>
public class ProductService
{
    public List<Models.Product> LoadAllowedProducts(string filePath)
    {
        var products = new List<Models.Product>();
        if (!File.Exists(filePath)) return products;

        foreach (var line in File.ReadAllLines(filePath).Skip(1)) // skip the header line
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // ProductId,Name,Category,Brand,Unit,Calories,Protein,Carbs,Fat,Fiber,Sugar,Salt
            var parts = line.Split(',');
            if (parts.Length < 12 || !int.TryParse(parts[0], out int id)) continue;

            products.Add(new Models.Product
            {
                ProductId = id,
                Name = parts[1],
                Category = parts[2],
                Brand = parts[3],
                Unit = parts[4],
                CaloriesPer100 = Number(parts[5]),
                ProteinPer100 = Number(parts[6]),
                CarbsPer100 = Number(parts[7]),
                FatPer100 = Number(parts[8]),
                FiberPer100 = Number(parts[9]),
                SugarPer100 = Number(parts[10]),
                SaltPer100 = Number(parts[11])
            });
        }

        return products;
    }

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
}
