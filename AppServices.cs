using System.IO;
using System.Windows;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI;

/// <summary>
/// Central shared service instances acting as the single source of truth for all views:
/// the same pantry, receipts, shopping list, products and recipes everywhere.
/// </summary>
public static class AppServices
{
    public static ProductService Products { get; } = new();
    public static RecipeService Recipes { get; } = new();
    public static ReceiptService Receipts { get; } = new();
    public static PantryService Pantry { get; } = new();
    public static ShoppingListService Shopping { get; } = new();
    public static NutritionAnalyticsService Analytics { get; } = new();

    /// <summary>Unit system chosen in the recipe detail (kept while the application runs).</summary>
    public static UnitSystem DisplayUnits { get; set; } = UnitSystem.AsWritten;

    private static List<Product>? _cachedProducts;
    private static ProductMatcher? _cachedMatcher;
    private static List<Recipe>? _cachedRecipes;

    public static List<Product> GetAllowedProducts()
    {
        if (_cachedProducts == null || _cachedProducts.Count == 0)
        {
            try
            {
                _cachedProducts = File.Exists(AppPaths.ProductsPath)
                    ? Products.LoadAllowedProducts(AppPaths.ProductsPath)
                    : new List<Product>();
            }
            catch
            {
                _cachedProducts = new List<Product>();
            }

            _cachedMatcher = null;
        }

        return _cachedProducts;
    }

    /// <summary>Resolves typed ingredient / product names to products (case, accent and plural tolerant).</summary>
    public static ProductMatcher GetProductMatcher() => _cachedMatcher ??= new ProductMatcher(GetAllowedProducts());

    /// <summary>The recipes as last loaded. Ingredients are already linked to products.</summary>
    public static List<Recipe> GetLoadedRecipes() => _cachedRecipes ?? ReloadRecipes();

    /// <summary>Reads Recipes.txt again (for example after a recipe was added) and replaces the shared list.</summary>
    public static List<Recipe> ReloadRecipes()
    {
        _cachedRecipes = File.Exists(AppPaths.RecipesPath)
            ? Recipes.LoadRecipesFromTxt(AppPaths.RecipesPath, GetAllowedProducts())
            : new List<Recipe>();

        return _cachedRecipes;
    }

    /// <summary>Navigates to the Recipes screen with this recipe's detail panel open.</summary>
    public static void OpenRecipe(Recipe recipe)
    {
        if (Application.Current?.MainWindow is MainWindow window)
            window.OpenRecipe(recipe.RecipeId);
    }
}
