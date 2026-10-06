using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Nutrition for a chosen number of servings.
///
/// Recipe.CaloriesPerServing (and Protein/Carbs/FatPerServing) are values for ONE serving, so they never change when
/// the servings slider moves. The total is simply  per-serving value x selected servings.
/// (Ingredient quantities scale by selected / base servings; nutrition totals scale by selected servings.)
/// Values the recipe does not have are left out - nothing is estimated.
/// </summary>
public static class RecipeNutritionCalculator
{
    public static RecipeNutrition Calculate(Recipe recipe, int selectedServings)
    {
        int servings = Math.Max(selectedServings, 0);
        var lines = new List<NutritionLine>();
        bool hasMacros = false;

        if (recipe.CaloriesPerServing > 0)
            lines.Add(new NutritionLine("Calories", recipe.CaloriesPerServing, recipe.CaloriesPerServing * servings, "kcal", "0"));

        void AddMacro(string label, double? perServing)
        {
            if (!perServing.HasValue) return;
            lines.Add(new NutritionLine(label, perServing.Value, perServing.Value * servings, "g", "0.#"));
            hasMacros = true;
        }

        AddMacro("Protein", recipe.ProteinPerServing);
        AddMacro("Carbohydrates", recipe.CarbsPerServing);
        AddMacro("Fat", recipe.FatPerServing);

        return new RecipeNutrition
        {
            SelectedServings = servings,
            Lines = lines,
            HasMacros = hasMacros
        };
    }
}
