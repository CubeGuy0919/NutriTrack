using System.Globalization;
using System.Text;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Loads and saves the pantry (Pantry.txt: ProductId,Name,Quantity,Unit). Quantities are stored in g / ml / count unit,
/// so 1.5 kg typed by the user is kept as 1500 g and can be compared with any recipe unit.
/// </summary>
public class PantryService
{
    public const string FileHeader = "ProductId,Name,Quantity,Unit";

    public List<PantryItem> Load(string filePath)
    {
        var items = new List<PantryItem>();
        if (!File.Exists(filePath)) return items;

        foreach (var line in File.ReadAllLines(filePath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 4) continue;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int productId)) continue;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double quantity) || quantity <= 0) continue;

            items.Add(new PantryItem
            {
                ProductId = productId,
                Name = TextFile.Unescape(parts[1]),
                Quantity = quantity,
                Unit = parts[3].Trim()
            });
        }

        return items;
    }

    public void Save(string filePath, IEnumerable<PantryItem> items)
    {
        var sb = new StringBuilder();
        sb.Append(FileHeader).Append("\r\n");

        foreach (var item in items)
        {
            sb.Append(item.ProductId.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(TextFile.Escape(item.Name)).Append(',')
              .Append(item.Quantity.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(TextFile.Escape(item.Unit)).Append("\r\n");
        }

        TextFile.WriteAtomic(filePath, sb.ToString());
    }

    /// <summary>
    /// Adds stock for a product (any unit; it is converted to g / ml / count). Adding to a product that is already
    /// in the pantry increases its quantity. Returns false when the quantity is not positive.
    /// </summary>
    public static bool Add(List<PantryItem> items, int productId, string name, double quantity, string? unit)
    {
        if (!(quantity > 0) || double.IsInfinity(quantity)) return false;

        var amount = UnitConverter.ToBase(quantity, unit);
        string storedUnit = amount.BaseUnit;

        var existing = items.FirstOrDefault(i =>
            i.ProductId == productId && string.Equals(i.Unit, storedUnit, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Quantity = Math.Round(existing.Quantity + amount.Value, 2);
        }
        else
        {
            items.Add(new PantryItem
            {
                ProductId = productId,
                Name = name,
                Quantity = Math.Round(amount.Value, 2),
                Unit = storedUnit
            });
        }

        return true;
    }
}
