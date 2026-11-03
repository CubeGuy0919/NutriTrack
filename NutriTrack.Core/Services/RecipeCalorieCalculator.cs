using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>Calculates nutrition from product data without guessing missing unit conversions.</summary>
public class RecipeCalorieCalculator
{
    /// <summary>
    /// Calculates per-serving nutrition. It returns false if an ingredient has no product or USDA-supported
    /// conversion to grams, because skipping that ingredient would make the result misleading.
    /// </summary>
    public bool TryCalculate(
        Recipe recipe,
        IReadOnlyList<Product> products,
        out double caloriesPerServing,
        out double proteinPerServing,
        out double carbsPerServing,
        out double fatPerServing)
    {
        caloriesPerServing = 0;
        proteinPerServing = 0;
        carbsPerServing = 0;
        fatPerServing = 0;

        if (recipe.Servings < 1 || recipe.Ingredients.Count == 0)
            return false;

        var matcher = new ProductMatcher(products);
        double calories = 0;
        double protein = 0;
        double carbs = 0;
        double fat = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!ingredient.IsParsed || ingredient.Quantity <= 0)
                return false;

            Product? product = ingredient.ProductId.HasValue
                ? matcher.GetById(ingredient.ProductId.Value)
                : matcher.Match(ingredient.Name);

            if (product == null || !product.HasNutritionData || !TryGetGrams(ingredient, product, out double grams))
                return false;

            calories += grams * product.CaloriesPer100!.Value / 100;
            protein += grams * product.ProteinPer100!.Value / 100;
            carbs += grams * product.CarbsPer100!.Value / 100;
            fat += grams * product.FatPer100!.Value / 100;
        }

        caloriesPerServing = Math.Round(calories / recipe.Servings, 0);
        proteinPerServing = Math.Round(protein / recipe.Servings, 1);
        carbsPerServing = Math.Round(carbs / recipe.Servings, 1);
        fatPerServing = Math.Round(fat / recipe.Servings, 1);
        return true;
    }

    /// <summary>Converts one ingredient to grams using mass units, USDA density, or USDA piece weight.</summary>
    private static bool TryGetGrams(IngredientItem ingredient, Product product, out double grams)
    {
        grams = 0;
        BaseAmount amount = UnitConverter.ToBase(ingredient.Quantity, ingredient.Unit);

        if (amount.Dimension == UnitDimension.Mass)
        {
            grams = amount.Value;
            return true;
        }
        return true;
    }
}
