using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// The only place where ingredient quantities are multiplied for a number of servings.
/// Pipeline: original quantity -> x scaleFactor -> (unit handling in <see cref="UnitConverter"/>) -> display formatting.
/// Works on <see cref="IngredientItem"/>, so old Recipes.txt recipes and newly added recipes scale identically.
/// </summary>
public static class IngredientScaler
{
    public static IReadOnlyList<ScaledIngredient> Scale(Recipe recipe, int selectedServings)
    {
        double factor = recipe.GetScaleFactor(selectedServings);
        return recipe.Ingredients.Select(i => Scale(i, factor)).ToList();
    }

    public static ScaledIngredient Scale(IngredientItem item, double scaleFactor)
    {
        // "salt to taste", "oil for frying": no number to multiply, so the original text is kept as it is
        if (!item.IsScalable)
            return new ScaledIngredient(item, 0, item.Unit, item.DisplayText, string.Empty);

        double quantity = item.Quantity * Math.Max(scaleFactor, 0);

        string number = QuantityFormatter.Format(quantity, item.Unit);
        string unit = UnitConverter.DisplayUnit(item.Unit, quantity);
        string label = unit.Length > 0 ? $"{number} {unit}" : number;

        // "2 eggs" scaled down to one reads "1 egg", not "1 eggs"
        string name = item.Unit.Length == 0 && number == "1"
            ? ProductMatcher.Singularize(item.Name)
            : item.Name;

        string text = string.IsNullOrWhiteSpace(name) ? label : $"{label} {name}";
        return new ScaledIngredient(item, quantity, item.Unit, text, label);
    }
}
