using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Scales a recipe's ingredients to a number of servings. Three steps, always in this order:
///   1. multiply the quantity by (selected servings / base servings)
///   2. convert it to the chosen unit system (Metric / Imperial / as written)
///   3. format it as text ("750 g", "1 1/2 tbsp")
/// ScaledIngredient.Quantity stays in the recipe's own unit, so pantry and shopping maths never depend on the display system.
/// </summary>
public static class IngredientScaler
{
    public static IReadOnlyList<ScaledIngredient> Scale(Recipe recipe, int selectedServings, UnitSystem system = UnitSystem.AsWritten)
    {
        double factor = recipe.GetScaleFactor(selectedServings);
        return recipe.Ingredients.Select(i => Scale(i, factor, system)).ToList();
    }

    public static ScaledIngredient Scale(IngredientItem item, double scaleFactor, UnitSystem system = UnitSystem.AsWritten)
    {
        if (!item.IsScalable)
            return new ScaledIngredient(item, 0, item.Unit, item.DisplayText, string.Empty);

        double quantity = item.Quantity * Math.Max(scaleFactor, 0);

        var (displayQuantity, displayUnit) = UnitConverter.ConvertForDisplay(quantity, item.Unit, system);
        string number = QuantityFormatter.Format(displayQuantity, displayUnit);
        string unitWord = UnitConverter.DisplayUnit(displayUnit, displayQuantity);
        string label = unitWord.Length > 0 ? $"{number} {unitWord}" : number;

        string name = displayUnit.Length == 0 && number == "1"
            ? ProductMatcher.Singularize(item.Name)
            : item.Name;

        string text = string.IsNullOrWhiteSpace(name) ? label : $"{label} {name}";
        return new ScaledIngredient(item, quantity, item.Unit, text, label);
    }
}
