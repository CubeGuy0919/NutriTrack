using System.Globalization;

namespace NutriTrack.Core.Models;

/// <summary>
/// One structured recipe ingredient, e.g. "500 g Chicken Breast".
/// Quantity is always stored for the recipe's BASE servings, so scaling is a
/// plain multiplication: <c>Quantity * scaleFactor</c> (see <see cref="Scale"/>).
/// </summary>
public class IngredientItem
{
    private double _quantity;
    private string _unit = string.Empty;
    private string _name = string.Empty;

    /// <summary>
    /// The text this item was parsed from. It lets an untouched legacy ingredient be written back
    /// to Recipes.txt exactly as it was read. Any edit to Quantity/Unit/Name clears it.
    /// </summary>
    public string? SourceText { get; internal set; }

    /// <summary>Id of the matching <see cref="Product"/>, or null when no product could be identified.</summary>
    public int? ProductId { get; set; }

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; SourceText = null; }
    }

    public double Quantity
    {
        get => _quantity;
        set { _quantity = value; SourceText = null; }
    }

    /// <summary>Unit such as "g", "ml", "tbsp", "clove". Empty for countable items ("2 eggs").</summary>
    public string Unit
    {
        get => _unit;
        set { _unit = value?.Trim() ?? string.Empty; SourceText = null; }
    }

    /// <summary>
    /// False when the original text could not be understood (e.g. "salt to taste").
    /// Name then holds the original text, Quantity is 0 and the item is not scalable.
    /// </summary>
    public bool IsParsed { get; internal set; } = true;

    public bool IsScalable => IsParsed && Quantity > 0;

    public bool HasProduct => ProductId.HasValue;

    /// <summary>Just the amount part, e.g. "500 g", "2", "1/2 tsp". Empty when the item has no quantity.</summary>
    public string QuantityLabel => IsParsed && Quantity > 0
        ? string.Join(' ', new[] { FormatQuantity(Quantity), Unit }.Where(x => !string.IsNullOrWhiteSpace(x)))
        : string.Empty;

    /// <summary>Human readable line, e.g. "600 g ripe tomatoes", "1/2 tsp salt", "2 large onions".</summary>
    public string DisplayText => ToText(Quantity);

    /// <summary>
    /// Returns a copy with the quantity multiplied by <paramref name="scaleFactor"/>
    /// (selected servings / base servings). Items that are not scalable are returned unchanged.
    /// </summary>
    public IngredientItem Scale(double scaleFactor)
    {
        var copy = Clone();
        if (IsScalable)
            copy.Quantity = Quantity * scaleFactor; // setter clears SourceText
        return copy;
    }

    public IngredientItem Clone() => new()
    {
        _quantity = _quantity,
        _unit = _unit,
        _name = _name,
        SourceText = SourceText,
        ProductId = ProductId,
        IsParsed = IsParsed
    };

    /// <summary>Text used when writing to Recipes.txt. Lossless for ingredients that were never edited.</summary>
    public string ToStorageString() => SourceText ?? DisplayText;

    public override string ToString() => DisplayText;

    private string ToText(double quantity)
    {
        if (!IsParsed) return SourceText ?? Name;

        var parts = new List<string>(3);
        if (quantity > 0) parts.Add(FormatQuantity(quantity));
        if (!string.IsNullOrWhiteSpace(Unit)) parts.Add(Unit);
        if (!string.IsNullOrWhiteSpace(Name)) parts.Add(Name);
        return string.Join(' ', parts);
    }

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
