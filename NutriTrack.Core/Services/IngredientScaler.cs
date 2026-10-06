using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// The only place where ingredient quantities are multiplied for a number of servings.
/// Pipeline: original quantity -> x scaleFactor -> unit conversion (display only) -> display formatting.
/// Works on <see cref="IngredientItem"/>, so old Recipes.txt recipes and newly added recipes scale identically.
/// ScaledIngredient.Quantity / Unit always stay in the recipe's own unit, so pantry and shopping maths never depend on the display system.
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
        // "salt to taste", "oil for frying": no number to multiply, so the original text is kept as it is
        if (!item.IsScalable)
            return new ScaledIngredient(item, 0, item.Unit, item.DisplayText, string.Empty);

        double quantity = item.Quantity * Math.Max(scaleFactor, 0);

        var (displayQuantity, displayUnit) = UnitConverter.ConvertForDisplay(quantity, item.Unit, system);
        string number = QuantityFormatter.Format(displayQuantity, displayUnit);
        string unit = UnitConverter.DisplayUnit(displayUnit, displayQuantity);
        string label = unit.Length > 0 ? $"{number} {unit}" : number;

        // "2 eggs" scaled down to one reads "1 egg", not "1 eggs"
        string name = displayUnit.Length == 0 && number == "1"
            ? ProductMatcher.Singularize(item.Name)
            : item.Name;

        string text = string.IsNullOrWhiteSpace(name) ? label : $"{label} {name}";
        return new ScaledIngredient(item, quantity, item.Unit, text, label);
    }
}
