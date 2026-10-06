using System.Globalization;

namespace NutriTrack.Core.Services;

/// <summary>
/// Turns a scaled number into readable text, with precision chosen by unit:
/// 333.333 g gives "333", 0.5 kg gives "0.5", 1.5 tbsp gives "1 1/2", 0.5 of an onion gives "1/2".
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

        string u = (unit ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

        switch (u)
        {
            case "g":
            case "ml":
            case "mg":
                // 333.333 -> 333, 62.5 -> 62.5, 0.25 -> 0.25
                return quantity >= 100 ? RoundedText(quantity, 0)
                     : quantity >= 1 ? RoundedText(quantity, 1)
                     : RoundedText(quantity, 2);

            case "kg":
            case "l":
            case "dl":
            case "cl":
            case "lb":
            case "oz":
                return RoundedText(quantity, 2);

            case "fl oz":
                return RoundedText(quantity, 1);

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

    /// <summary>
    /// Complete amount text for a unit system: converts, formats and adds the unit word, e.g. "26 oz", "1 1/2 tbsp", "750 g".
    /// </summary>
    public static string FormatAmount(double quantity, string? unit, UnitSystem system)
    {
        var (displayQuantity, displayUnit) = UnitConverter.ConvertForDisplay(quantity, unit, system);
        string number = Format(displayQuantity, displayUnit);
        string word = UnitConverter.DisplayUnit(displayUnit, displayQuantity);
        return word.Length > 0 ? $"{number} {word}" : number;
    }

    private static string RoundedText(double value, int decimals)
    {
        double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        string format = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return rounded.ToString(format, CultureInfo.InvariantCulture);
    }

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

        if (best.Value >= 1)
        {
            whole += 1;
            best = steps[0];
        }

        if (best.Text.Length == 0)
        {
            // A whole number. Anything too small to show as a fraction falls back to a decimal.
            return whole > 0 ? whole.ToString("0", CultureInfo.InvariantCulture) : RoundedText(quantity, 2);
        }

        return whole > 0
            ? $"{whole.ToString("0", CultureInfo.InvariantCulture)} {best.Text}"
            : best.Text;
    }
}
