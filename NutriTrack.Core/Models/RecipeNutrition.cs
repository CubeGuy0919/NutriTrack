using System.Globalization;

namespace NutriTrack.Core.Models;

/// <summary>One nutrition row: the per-serving value from the recipe and the total for the selected servings.</summary>
public class NutritionLine
{
    public NutritionLine(string label, double perServing, double total, string unit, string format)
    {
        Label = label;
        PerServing = perServing;
        Total = total;
        Unit = unit;
        _format = format;
    }

    private readonly string _format;

    public string Label { get; }

    /// <summary>Value for ONE serving. Never changes when the servings slider moves.</summary>
    public double PerServing { get; }

    /// <summary>PerServing x selected servings.</summary>
    public double Total { get; }

    public string Unit { get; }

    public string PerServingText => $"{PerServing.ToString(_format, CultureInfo.InvariantCulture)} {Unit}";
    public string TotalText => $"{Total.ToString(_format, CultureInfo.InvariantCulture)} {Unit}";
}

/// <summary>Nutrition for a recipe at a chosen number of servings. Only values the recipe really has are included.</summary>
public class RecipeNutrition
{
    public int SelectedServings { get; init; }
    public IReadOnlyList<NutritionLine> Lines { get; init; } = Array.Empty<NutritionLine>();

    /// <summary>True when only calories (or nothing) are known, so the UI can say that macros were not provided.</summary>
    public bool HasMacros { get; init; }
}
