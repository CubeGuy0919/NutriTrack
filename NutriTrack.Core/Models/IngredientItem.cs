using System.Globalization;

namespace NutriTrack.Core.Models;

/// <summary>
/// One structured recipe ingredient, e.g. "500 g Chicken Breast" = Quantity 500, Unit "g", Name "Chicken Breast".
/// The quantity is always written for the recipe's own number of servings; scaling is done by IngredientScaler.
/// </summary>
public class IngredientItem
{
    /// <summary>Id of the matching <see cref="Product"/>, or null when no product could be identified.</summary>
    public int? ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Quantity { get; set; }

    /// <summary>Unit such as "g", "ml", "tbsp", "clove". Empty for countable items ("2 eggs").</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// False when the original text could not be understood (e.g. "salt to taste").
    /// Name then holds the original text, Quantity is 0 and the item is not scalable.
    /// </summary>
    public bool IsParsed { get; internal set; } = true;

    public bool IsScalable => IsParsed && Quantity > 0;

    public bool HasProduct => ProductId.HasValue;

    /// <summary>Just the amount, e.g. "500 g", "2", "1/2 tsp". Empty when the item has no quantity.</summary>
    public string QuantityLabel => IsScalable
        ? string.Join(' ', new[] { FormatQuantity(Quantity), Unit }.Where(x => !string.IsNullOrWhiteSpace(x)))
        : string.Empty;

    /// <summary>Human readable line, e.g. "600 g ripe tomatoes", "1/2 tsp salt". This is also what is written to Recipes.txt.</summary>
    public string DisplayText
    {
        get
        {
            if (!IsParsed) return Name;

            var parts = new List<string>(3);
            if (Quantity > 0) parts.Add(FormatQuantity(Quantity));
            if (!string.IsNullOrWhiteSpace(Unit)) parts.Add(Unit);
            if (!string.IsNullOrWhiteSpace(Name)) parts.Add(Name);
            return string.Join(' ', parts);
        }
    }

    public IngredientItem Clone() => new()
    {
        ProductId = ProductId,
        Name = Name,
        Quantity = Quantity,
        Unit = Unit,
        IsParsed = IsParsed
    };

    public override string ToString() => DisplayText;

    /// <summary>Formats 0.5 as "1/2", 1.5 as "1 1/2", 600 as "600", 0.3 as "0.3".</summary>
    public static string FormatQuantity(double quantity)
    {
        if (quantity <= 0) return "0";

        double whole = Math.Floor(quantity + 1e-9);
        double fraction = quantity - whole;

        if (fraction < 0.005)
            return whole.ToString("0", CultureInfo.InvariantCulture);

        (double Value, string Text)[] known =
        {
            (1.0 / 8, "1/8"), (1.0 / 4, "1/4"), (1.0 / 3, "1/3"), (3.0 / 8, "3/8"), (1.0 / 2, "1/2"),
            (5.0 / 8, "5/8"), (2.0 / 3, "2/3"), (3.0 / 4, "3/4"), (7.0 / 8, "7/8")
        };

        foreach (var (value, text) in known)
        {
            if (Math.Abs(fraction - value) < 0.005)
                return whole > 0
                    ? $"{whole.ToString("0", CultureInfo.InvariantCulture)} {text}"
                    : text;
        }

        return quantity.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
