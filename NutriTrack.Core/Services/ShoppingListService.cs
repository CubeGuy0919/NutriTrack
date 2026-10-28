using System.Globalization;
using System.Text;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>Loads, saves and fills the shopping list (ShoppingList.txt: ProductId,Name,Quantity,Unit).</summary>
public class ShoppingListService
{
    public const string FileHeader = "ProductId,Name,Quantity,Unit";

    public List<ShoppingListItem> Load(string filePath)
    {
        var items = new List<ShoppingListItem>();
        if (!File.Exists(filePath)) return items;

        foreach (var line in File.ReadAllLines(filePath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 4) continue;

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double quantity) || quantity <= 0) continue;

            items.Add(new ShoppingListItem
            {
                ProductId = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : null,
                Name = TextFile.Unescape(parts[1]),
                Quantity = quantity,
                Unit = parts[3].Trim()
            });
        }

        return items;
    }

    public void Save(string filePath, IEnumerable<ShoppingListItem> items)
    {
        var sb = new StringBuilder();
        sb.Append(FileHeader).Append("\r\n");

        foreach (var item in items)
        {
            sb.Append(item.ProductId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
              .Append(TextFile.Escape(item.Name)).Append(',')
              .Append(item.Quantity.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(TextFile.Escape(item.Unit)).Append("\r\n");
        }

        TextFile.WriteAtomic(filePath, sb.ToString());
    }

    /// <summary>
    /// Adds everything that still has to be bought. Only the shortfall is added for partly available ingredients,
    /// the full amount for the others. An item already on the list (same product / name, compatible unit) is increased
    /// instead of duplicated. Returns how many ingredients were added or increased.
    /// </summary>
    public int AddMissing(List<ShoppingListItem> list, IEnumerable<IngredientAvailability> availability)
    {
        int changed = 0;

        foreach (var entry in availability)
        {
            if (!entry.NeedsPurchase) continue;

            double quantity = Math.Round(entry.QuantityToBuy, 2, MidpointRounding.AwayFromZero);
            if (!(quantity > 0)) continue;

            string unit = entry.Ingredient.Source.Unit;
            var added = UnitConverter.ToBase(quantity, unit);

            var existing = list.FirstOrDefault(i =>
                SameIngredient(i, entry.Ingredient) &&
                UnitConverter.AreCompatible(UnitConverter.ToBase(i.Quantity, i.Unit), added));

            if (existing != null)
            {
                existing.Quantity = Math.Round(existing.Quantity + UnitConverter.FromBase(added.Value, existing.Unit), 2);
            }
            else
            {
                list.Add(new ShoppingListItem
                {
                    ProductId = entry.Ingredient.ProductId,
                    Name = entry.Ingredient.Name,
                    Quantity = quantity,
                    Unit = unit
                });
            }

            changed++;
        }

        return changed;
    }

    private static bool SameIngredient(ShoppingListItem item, ScaledIngredient ingredient)
    {
        if (item.ProductId.HasValue && ingredient.ProductId.HasValue)
            return item.ProductId.Value == ingredient.ProductId.Value;

        if (item.ProductId.HasValue != ingredient.ProductId.HasValue)
            return false;

        return ProductMatcher.Normalize(item.Name) == ProductMatcher.Normalize(ingredient.Name);
    }
}
