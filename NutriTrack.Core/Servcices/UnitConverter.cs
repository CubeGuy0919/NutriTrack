namespace NutriTrack.Core.Services;

public enum UnitDimension
{
    Mass,
    Volume,
    /// <summary>Countable things: pieces, cloves, slices ... Only comparable when the count unit is the same.</summary>
    Count
}

/// <summary>An amount expressed in the base unit of its dimension: grams, millilitres, or a count unit.</summary>
public readonly record struct BaseAmount(UnitDimension Dimension, double Value, string CountKey)
{
    public string BaseUnit => Dimension switch
    {
        UnitDimension.Mass => "g",
        UnitDimension.Volume => "ml",
        _ => CountKey
    };
}

/// <summary>
/// The single place that knows about units: which dimension a unit belongs to, how to convert it to the
/// base unit (g / ml / count) and back, and how a unit word is shown ("clove" vs "cloves").
/// Used for pantry comparison and shopping-list merging. It never changes what the recipe itself stores.
/// </summary>
public static class UnitConverter
{
    private static readonly Dictionary<string, double> MassFactors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = 1, ["kg"] = 1000, ["mg"] = 0.001, ["oz"] = 28.3495, ["lb"] = 453.592
    };

    private static readonly Dictionary<string, double> VolumeFactors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ml"] = 1, ["l"] = 1000, ["dl"] = 100, ["cl"] = 10,
        ["tsp"] = 5, ["tbsp"] = 15, ["cup"] = 240, ["cups"] = 240, ["fl oz"] = 29.5735
    };

    private static readonly Dictionary<string, string> CountSingulars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pc"] = "pcs", ["pcs"] = "pcs", ["piece"] = "pcs", ["pieces"] = "pcs",
        ["clove"] = "clove", ["cloves"] = "clove",
        ["slice"] = "slice", ["slices"] = "slice",
        ["pinch"] = "pinch", ["pinches"] = "pinch",
        ["dash"] = "dash", ["dashes"] = "dash",
        ["sprig"] = "sprig", ["sprigs"] = "sprig",
        ["stalk"] = "stalk", ["stalks"] = "stalk",
        ["can"] = "can", ["cans"] = "can",
        ["bunch"] = "bunch", ["bunches"] = "bunch",
        ["sheet"] = "sheet", ["sheets"] = "sheet"
    };

    // Units that read differently for one and for several: (singular, plural)
    private static readonly Dictionary<string, (string Singular, string Plural)> PluralForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clove"] = ("clove", "cloves"), ["cloves"] = ("clove", "cloves"),
        ["slice"] = ("slice", "slices"), ["slices"] = ("slice", "slices"),
        ["pinch"] = ("pinch", "pinches"), ["pinches"] = ("pinch", "pinches"),
        ["dash"] = ("dash", "dashes"), ["dashes"] = ("dash", "dashes"),
        ["sprig"] = ("sprig", "sprigs"), ["sprigs"] = ("sprig", "sprigs"),
        ["stalk"] = ("stalk", "stalks"), ["stalks"] = ("stalk", "stalks"),
        ["can"] = ("can", "cans"), ["cans"] = ("can", "cans"),
        ["bunch"] = ("bunch", "bunches"), ["bunches"] = ("bunch", "bunches"),
        ["sheet"] = ("sheet", "sheets"), ["sheets"] = ("sheet", "sheets"),
        ["cup"] = ("cup", "cups"), ["cups"] = ("cup", "cups")
    };

    private static string Clean(string? unit) => (unit ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    /// <summary>Converts an amount to grams, millilitres or a count. Unknown or empty units count as pieces.</summary>
    public static BaseAmount ToBase(double quantity, string? unit)
    {
        string u = Clean(unit);

        if (MassFactors.TryGetValue(u, out double mass))
            return new BaseAmount(UnitDimension.Mass, quantity * mass, "g");

        if (VolumeFactors.TryGetValue(u, out double volume))
            return new BaseAmount(UnitDimension.Volume, quantity * volume, "ml");

        return new BaseAmount(UnitDimension.Count, quantity, CountKey(u));
    }

    /// <summary>Converts a base-unit value (g / ml / count) back to the given unit.</summary>
    public static double FromBase(double baseValue, string? unit)
    {
        string u = Clean(unit);

        if (MassFactors.TryGetValue(u, out double mass)) return baseValue / mass;
        if (VolumeFactors.TryGetValue(u, out double volume)) return baseValue / volume;
        return baseValue;
    }

    /// <summary>True when the two amounts can be added or subtracted (same dimension, and same count unit for counts).</summary>
    public static bool AreCompatible(BaseAmount a, BaseAmount b) =>
        a.Dimension == b.Dimension &&
        (a.Dimension != UnitDimension.Count || string.Equals(a.CountKey, b.CountKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Normalised name of a count unit: "" / "piece" / "pieces" become "pcs", "cloves" becomes "clove".</summary>
    public static string CountKey(string? unit)
    {
        string u = Clean(unit);
        if (u.Length == 0) return "pcs";
        return CountSingulars.TryGetValue(u, out var key) ? key : u;
    }

    /// <summary>Unit word to show next to a number: "clove" for 1 or less, "cloves" above. Other units are returned unchanged.</summary>
    public static string DisplayUnit(string? unit, double quantity)
    {
        string trimmed = (unit ?? string.Empty).Trim();
        if (PluralForms.TryGetValue(Clean(trimmed), out var forms))
            return quantity <= 1.0000001 ? forms.Singular : forms.Plural;
        return trimmed;
    }
}
