using System.Globalization;
using System.Text.RegularExpressions;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Converts legacy ingredient strings from Recipes.txt ("600 g ripe tomatoes") into structured
/// <see cref="IngredientItem"/> objects. Never throws: text that cannot be understood is kept as-is
/// (IsParsed = false) so no data is lost.
/// </summary>
public static class IngredientParser
{
    // Quantity forms, tried in this order: mixed number (1 1/2), fraction (1/2), decimal (1.5 / 1,5), unicode fraction.
    // The lookahead stops things like "2-3 cloves" or "3x" from being half-parsed.
    private static readonly Regex QuantityRegex = new(
        @"^\s*(?:(?<mw>\d+)\s+(?<mn>\d+)/(?<md>\d+)|(?<fn>\d+)/(?<fd>\d+)|(?<dec>\d+(?:[.,]\d+)?)(?<uf>[½¼¾⅓⅔⅛])?|(?<uo>[½¼¾⅓⅔⅛]))(?=\s|$|[A-Za-z])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<char, double> UnicodeFractions = new()
    {
        ['½'] = 0.5, ['¼'] = 0.25, ['¾'] = 0.75, ['⅓'] = 1.0 / 3, ['⅔'] = 2.0 / 3, ['⅛'] = 0.125
    };

    /// <summary>
    /// Words that are real units of measure or portion words. Anything NOT in this table
    /// ("large", "medium", "red", "spring", "ripe" ...) stays part of the ingredient name.
    /// Key = what the user may type, value = the unit stored.
    /// </summary>
    private static readonly Dictionary<string, string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        // metric weight / volume
        ["g"] = "g", ["gram"] = "g", ["grams"] = "g", ["kg"] = "kg", ["kilogram"] = "kg", ["kilograms"] = "kg",
        ["mg"] = "mg",
        ["ml"] = "ml", ["milliliter"] = "ml", ["milliliters"] = "ml", ["millilitre"] = "ml", ["millilitres"] = "ml",
        ["l"] = "l", ["liter"] = "l", ["liters"] = "l", ["litre"] = "l", ["litres"] = "l",
        ["dl"] = "dl", ["cl"] = "cl",
        // spoons / cups
        ["tsp"] = "tsp", ["teaspoon"] = "tsp", ["teaspoons"] = "tsp",
        ["tbsp"] = "tbsp", ["tablespoon"] = "tbsp", ["tablespoons"] = "tbsp",
        ["cup"] = "cup", ["cups"] = "cups",
        // imperial
        ["oz"] = "oz", ["ounce"] = "oz", ["ounces"] = "oz", ["lb"] = "lb", ["lbs"] = "lb", ["pound"] = "lb", ["pounds"] = "lb",
        // portion words that behave like units
        ["clove"] = "clove", ["cloves"] = "cloves",
        ["slice"] = "slice", ["slices"] = "slices",
        ["pinch"] = "pinch", ["pinches"] = "pinches",
        ["dash"] = "dash", ["dashes"] = "dashes",
        ["pcs"] = "pcs", ["pc"] = "pcs", ["piece"] = "pcs", ["pieces"] = "pcs",
        ["sprig"] = "sprig", ["sprigs"] = "sprigs",
        ["stalk"] = "stalk", ["stalks"] = "stalks",
        ["can"] = "can", ["cans"] = "cans",
        ["bunch"] = "bunch", ["bunches"] = "bunches",
        ["sheet"] = "sheet", ["sheets"] = "sheets"
    };

    /// <summary>Units offered in the Add Recipe dialog.</summary>
    public static IReadOnlyList<string> CommonUnits { get; } = new[]
    {
        "g", "kg", "ml", "l", "tsp", "tbsp", "cup", "oz", "lb", "pcs", "clove", "slice", "pinch"
    };

    /// <summary>Parses one ingredient line. Never returns null and never throws.</summary>
    public static IngredientItem Parse(string? text)
    {
        string original = text?.Trim() ?? string.Empty;
        try
        {
            if (original.Length > 0 && TryParseCore(original, out var item))
                return item;
        }
        catch
        {
            // fall through to the safe fallback below
        }

        return new IngredientItem { Name = original }.AsUnparsed(original);
    }

    /// <summary>True when the text was understood as quantity [unit] name.</summary>
    public static bool TryParse(string? text, out IngredientItem item)
    {
        item = Parse(text);
        return item.IsParsed;
    }

    /// <summary>
    /// Parses a quantity typed by the user: "500", "1.5", "1,5", "1/2", "1 1/2".
    /// Returns false for empty, non-numeric or non-positive input.
    /// </summary>
    public static bool TryParseQuantity(string? text, out double quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        var match = QuantityRegex.Match(trimmed);
        if (!match.Success || match.Length != trimmed.Length)
            return false; // empty, not a number, or has trailing junk such as "5 kg"

        return TryReadQuantity(match, out quantity) && quantity > 0;
    }

    /// <summary>Maps typed unit text ("Tablespoons", "pcs.") to the stored unit, or returns the trimmed text for unknown units.</summary>
    public static string NormalizeUnit(string? unit)
    {
        string trimmed = (unit ?? string.Empty).Trim().TrimEnd('.');
        return KnownUnits.TryGetValue(trimmed, out var known) ? known : trimmed;
    }

    // ---------------------------------------------------------------

    private static bool TryParseCore(string line, out IngredientItem item)
    {
        item = null!;

        var match = QuantityRegex.Match(line);
        if (!match.Success) return false;
        if (!TryReadQuantity(match, out double quantity)) return false;

        string rest = line.Substring(match.Length).Trim();
        string unit = string.Empty;

        // "fl oz" is the only two-word unit we support
        if (rest.StartsWith("fl oz", StringComparison.OrdinalIgnoreCase) &&
            (rest.Length == 5 || char.IsWhiteSpace(rest[5])))
        {
            unit = "fl oz";
            rest = rest.Substring(5).Trim();
        }
        else if (rest.Length > 0)
        {
            int space = IndexOfWhitespace(rest);
            string firstWord = space < 0 ? rest : rest.Substring(0, space);
            string candidate = firstWord.TrimEnd('.');

            if (KnownUnits.TryGetValue(candidate, out var known))
            {
                unit = known;
                rest = space < 0 ? string.Empty : rest.Substring(space).Trim();
            }
        }

        if (rest.Length == 0 && unit.Length > 0) return false;   // "2 tbsp" with no ingredient name
        if (rest.Length == 0) return false;                       // just a number

        item = new IngredientItem { Quantity = quantity, Unit = unit, Name = rest };
        item.SourceText = line;
        return true;
    }

    private static IngredientItem AsUnparsed(this IngredientItem item, string original)
    {
        item.IsParsed = false;
        item.SourceText = original;
        return item;
    }

    private static int IndexOfWhitespace(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (char.IsWhiteSpace(s[i])) return i;
        return -1;
    }

    private static bool TryReadQuantity(Match m, out double quantity)
    {
        quantity = 0;

        if (m.Groups["mw"].Success)
        {
            if (!int.TryParse(m.Groups["mw"].Value, out int whole) ||
                !int.TryParse(m.Groups["mn"].Value, out int num) ||
                !int.TryParse(m.Groups["md"].Value, out int den) || den == 0) return false;
            quantity = whole + (double)num / den;
            return true;
        }

        if (m.Groups["fn"].Success)
        {
            if (!int.TryParse(m.Groups["fn"].Value, out int num) ||
                !int.TryParse(m.Groups["fd"].Value, out int den) || den == 0) return false;
            quantity = (double)num / den;
            return true;
        }

        if (m.Groups["dec"].Success)
        {
            string dec = m.Groups["dec"].Value.Replace(',', '.');
            if (!double.TryParse(dec, NumberStyles.Float, CultureInfo.InvariantCulture, out quantity)) return false;
            if (m.Groups["uf"].Success) quantity += UnicodeFractions[m.Groups["uf"].Value[0]];
            return true;
        }

        if (m.Groups["uo"].Success)
        {
            quantity = UnicodeFractions[m.Groups["uo"].Value[0]];
            return true;
        }

        return false;
    }
}
