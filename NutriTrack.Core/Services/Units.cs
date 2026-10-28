using System.Globalization;

namespace NutriTrack.Core.Services;

// Everything about units lives in this file. The idea in one sentence:
//   a recipe is scaled first, then converted to the chosen unit system, then formatted as text.
// Pantry and shopping-list maths never use the display system - they compare amounts in grams / ml / counts.

/// <summary>How amounts are shown in the recipe detail. Only the display changes, stored recipes never do.</summary>
public enum UnitSystem
{
    /// <summary>Exactly the units the recipe was written in.</summary>
    AsWritten,
    /// <summary>g / kg and ml / l. Ounces, pounds, cups and spoons are converted.</summary>
    Metric,
    /// <summary>oz / lb and fl oz. Grams, kilograms, litres and millilitres are converted.</summary>
    Imperial
}

public enum UnitDimension
{
    Mass,
    Volume,
    /// <summary>Countable things: pieces, cloves, slices ... Only comparable when the count word is the same.</summary>
    Count
}

/// <summary>An amount in the base unit of its dimension: grams, millilitres, or a count word.</summary>
public readonly record struct BaseAmount(UnitDimension Dimension, double Value, string CountKey)
{
    public string BaseUnit => Dimension switch
    {
        UnitDimension.Mass => "g",
        UnitDimension.Volume => "ml",
        _ => CountKey
    };
}

/// <summary>Knows which unit is mass / volume / count, converts between them, and picks the unit word to show.</summary>
public static class UnitConverter
{
    private const double GramsPerOunce = 28.3495;
    private const double GramsPerPound = 453.592;
    private const double MillilitresPerFluidOunce = 29.5735;

    // unit -> how many grams it is
    private static readonly Dictionary<string, double> MassUnits = new()
    {
        ["g"] = 1, ["kg"] = 1000, ["mg"] = 0.001, ["oz"] = GramsPerOunce, ["lb"] = GramsPerPound
    };

    // unit -> how many millilitres it is
    private static readonly Dictionary<string, double> VolumeUnits = new()
    {
        ["ml"] = 1, ["l"] = 1000, ["dl"] = 100, ["cl"] = 10,
        ["tsp"] = 5, ["tbsp"] = 15, ["cup"] = 240, ["cups"] = 240, ["fl oz"] = MillilitresPerFluidOunce
    };

    // Count words (singular form). The plural is the word + "s" or "es", handled in Singular / DisplayUnit.
    private static readonly string[] CountWords =
        { "clove", "slice", "pinch", "dash", "sprig", "stalk", "can", "bunch", "sheet" };

    private static string Clean(string? unit) =>
        (unit ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    // ---------------------------------------------------------------
    // For pantry and shopping list: compare amounts in a common base unit
    // ---------------------------------------------------------------

    /// <summary>Converts an amount to grams, millilitres or a count. Unknown or empty units count as pieces.</summary>
    public static BaseAmount ToBase(double quantity, string? unit)
    {
        string u = Clean(unit);

        if (MassUnits.TryGetValue(u, out double grams))
            return new BaseAmount(UnitDimension.Mass, quantity * grams, "g");

        if (VolumeUnits.TryGetValue(u, out double millilitres))
            return new BaseAmount(UnitDimension.Volume, quantity * millilitres, "ml");

        return new BaseAmount(UnitDimension.Count, quantity, CountKey(u));
    }

    /// <summary>Converts a base-unit value (g / ml / count) back to the given unit.</summary>
    public static double FromBase(double baseValue, string? unit)
    {
        string u = Clean(unit);

        if (MassUnits.TryGetValue(u, out double grams)) return baseValue / grams;
        if (VolumeUnits.TryGetValue(u, out double millilitres)) return baseValue / millilitres;
        return baseValue;
    }

    /// <summary>True when two amounts can be added or subtracted (same dimension, and same count word for counts).</summary>
    public static bool AreCompatible(BaseAmount a, BaseAmount b) =>
        a.Dimension == b.Dimension &&
        (a.Dimension != UnitDimension.Count || string.Equals(a.CountKey, b.CountKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Normalised count word: "" / "piece" / "pieces" become "pcs", "cloves" becomes "clove".</summary>
    public static string CountKey(string? unit)
    {
        string u = Clean(unit);
        if (u.Length == 0 || u is "pc" or "pcs" or "piece" or "pieces") return "pcs";
        return Singular(u);
    }

    /// <summary>"cloves" gives "clove", "pinches" gives "pinch". Words that are not count words are returned unchanged.</summary>
    private static string Singular(string word)
    {
        foreach (var countWord in CountWords)
        {
            if (word == countWord || word == countWord + "s" || word == countWord + "es")
                return countWord;
        }
        return word;
    }

    // ---------------------------------------------------------------
    // For the recipe screen: show an amount in the chosen unit system
    // ---------------------------------------------------------------

    /// <summary>
    /// Converts an amount for display. Counted units (pcs, clove ...) are never converted.
    ///   Metric   : oz, lb -> g / kg;  tsp, tbsp, cup, fl oz, dl, cl -> ml / l
    ///   Imperial : g, kg -> oz / lb;  ml, l, dl, cl -> fl oz   (tsp, tbsp and cups are already imperial)
    /// </summary>
    public static (double Quantity, string Unit) ConvertForDisplay(double quantity, string? unit, UnitSystem system)
    {
        string original = (unit ?? string.Empty).Trim();
        if (system == UnitSystem.AsWritten) return (quantity, original);

        string u = Clean(unit);
        var amount = ToBase(quantity, unit);

        // pieces, cloves ... and tiny mg amounts stay as they are
        if (amount.Dimension == UnitDimension.Count || u == "mg") return (quantity, original);

        if (system == UnitSystem.Metric)
        {
            return amount.Dimension == UnitDimension.Mass
                ? Promote(amount.Value, "g", "kg")
                : Promote(amount.Value, "ml", "l");
        }

        // Imperial
        if (amount.Dimension == UnitDimension.Mass)
        {
            if (u is "oz" or "lb") return (quantity, original);

            double ounces = amount.Value / GramsPerOunce;
            return ounces >= 16 ? (ounces / 16, "lb") : (ounces, "oz");
        }

        if (u is "tsp" or "tbsp" or "cup" or "cups" or "fl oz") return (quantity, original);
        return (amount.Value / MillilitresPerFluidOunce, "fl oz");
    }

    private static (double Quantity, string Unit) Promote(double value, string small, string large) =>
        value >= 1000 ? (value / 1000, large) : (value, small);

    /// <summary>Unit word to show next to a number: "clove" for 1 or less, "cloves" above, "cup" / "cups". Other units unchanged.</summary>
    public static string DisplayUnit(string? unit, double quantity)
    {
        string trimmed = (unit ?? string.Empty).Trim();
        string u = Clean(trimmed);
        bool singular = quantity <= 1.0000001;

        if (u is "cup" or "cups") return singular ? "cup" : "cups";

        string word = Singular(u);
        if (Array.IndexOf(CountWords, word) < 0) return trimmed;   // g, ml, tbsp, pcs ... have no plural form

        if (singular) return word;
        return word.EndsWith("ch") || word.EndsWith("sh") ? word + "es" : word + "s";
    }
}

/// <summary>
/// Turns a number into readable text, with precision chosen by unit:
/// 333.333 g gives "333", 0.5 kg gives "0.5", 1.5 tbsp gives "1 1/2", half an onion gives "1/2".
/// Display only - stored recipe quantities are never rounded.
/// </summary>
public static class QuantityFormatter
{
    private static readonly (double Value, string Text)[] EighthSteps =
    {
        (0, ""), (1.0 / 8, "1/8"), (1.0 / 4, "1/4"), (1.0 / 3, "1/3"), (3.0 / 8, "3/8"), (1.0 / 2, "1/2"),
        (5.0 / 8, "5/8"), (2.0 / 3, "2/3"), (3.0 / 4, "3/4"), (7.0 / 8, "7/8"), (1, "")
    };

    private static readonly (double Value, string Text)[] QuarterSteps =
    {
        (0, ""), (1.0 / 4, "1/4"), (1.0 / 3, "1/3"), (1.0 / 2, "1/2"), (2.0 / 3, "2/3"), (3.0 / 4, "3/4"), (1, "")
    };

    /// <summary>The number part only; the caller appends the unit.</summary>
    public static string Format(double quantity, string? unit)
    {
        if (double.IsNaN(quantity) || double.IsInfinity(quantity) || quantity <= 0)
            return "0";

        switch ((unit ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant())
        {
            case "g":
            case "ml":
            case "mg":
                // 333.333 -> 333, 62.5 -> 62.5, 0.25 -> 0.25
                return quantity >= 100 ? Round(quantity, 0)
                     : quantity >= 1 ? Round(quantity, 1)
                     : Round(quantity, 2);

            case "kg":
            case "l":
            case "dl":
            case "cl":
            case "lb":
            case "oz":
                return Round(quantity, 2);

            case "fl oz":
                return Round(quantity, 1);

            case "tsp":
            case "tbsp":
            case "cup":
            case "cups":
                return Fraction(quantity, EighthSteps);

            default:
                // pieces, cloves, slices, "2 tomatoes" (no unit) ...
                return Fraction(quantity, QuarterSteps);
        }
    }

    /// <summary>Complete amount text for a unit system: convert, format, add the unit word. For example "26 oz" or "1 1/2 tbsp".</summary>
    public static string FormatAmount(double quantity, string? unit, UnitSystem system)
    {
        var (displayQuantity, displayUnit) = UnitConverter.ConvertForDisplay(quantity, unit, system);
        string number = Format(displayQuantity, displayUnit);
        string word = UnitConverter.DisplayUnit(displayUnit, displayQuantity);
        return word.Length > 0 ? $"{number} {word}" : number;
    }

    private static string Round(double value, int decimals)
    {
        double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        string format = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return rounded.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>Whole part plus the nearest fraction from the list: 1.52 with quarters gives "1 1/2".</summary>
    private static string Fraction(double quantity, (double Value, string Text)[] steps)
    {
        double whole = Math.Floor(quantity);
        double fraction = quantity - whole;

        var best = steps[0];
        double bestDistance = double.MaxValue;
        foreach (var step in steps)
        {
            double distance = Math.Abs(step.Value - fraction);
            if (distance < bestDistance)
            {
                best = step;
                bestDistance = distance;
            }
        }

        // 0.97 is closest to a full 1: carry it into the whole part
        if (best.Value >= 1)
        {
            whole += 1;
            best = steps[0];
        }

        if (best.Text.Length == 0)
        {
            // A whole number. Anything too small to show as a fraction falls back to a decimal.
            return whole > 0 ? whole.ToString("0", CultureInfo.InvariantCulture) : Round(quantity, 2);
        }

        return whole > 0
            ? $"{whole.ToString("0", CultureInfo.InvariantCulture)} {best.Text}"
            : best.Text;
    }
}
