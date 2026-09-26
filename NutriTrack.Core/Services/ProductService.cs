using System.Globalization;
using System.IO;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class ProductService
{
    public List<Product> LoadAllowedProducts(string filePath)
    {
        var products = new List<Product>();

        if (!File.Exists(filePath))
            return products;

        var lines = File.ReadAllLines(filePath).Skip(1); // Skip header

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 12) continue;

            products.Add(new Product
            {
                ProductId = int.Parse(parts[0]),
                Name = parts[1],
                Category = parts[2],
                Brand = parts[3],
                Unit = parts[4],
                CaloriesPer100 = double.Parse(parts[5], CultureInfo.InvariantCulture),
                ProteinPer100 = double.Parse(parts[6], CultureInfo.InvariantCulture),
                CarbsPer100 = double.Parse(parts[7], CultureInfo.InvariantCulture),
                FatPer100 = double.Parse(parts[8], CultureInfo.InvariantCulture),
                FiberPer100 = double.Parse(parts[9], CultureInfo.InvariantCulture),
                SugarPer100 = double.Parse(parts[10], CultureInfo.InvariantCulture),
                SaltPer100 = double.Parse(parts[11], CultureInfo.InvariantCulture)
            });
        }

        return products;
    }
}