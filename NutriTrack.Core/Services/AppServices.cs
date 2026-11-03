using System.IO;
using System.Windows;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI;

/// <summary>
/// One shared set of services, so every screen sees the same pantry, receipts, shopping list, products and recipes.
/// (A simple stand-in for dependency injection: easy to explain, and enough for an app of this size.)
/// </summary>
public static class AppServices
{
    public static RecipeService Recipes { get; } = new();
    public static ReceiptService Receipts { get; } = new();
    public static PantryService Pantry { get; } = new();
    public static ShoppingListService Shopping { get; } = new();
    public static AnalyticsService Analytics { get; } = new();

    private static readonly ProductService ProductLoader = new();
    private static List<Product>? _products;
    private static ProductMatcher? _matcher;
    private static List<Recipe>? _recipes;

    /// <summary>The complete recipe-derived product catalogue. Blank nutrition fields are deliberately treated as unknown.</summary>
    public static List<Product> GetAllowedProducts()
    {
        if (_products == null || _products.Count == 0)
        {
            try
            {
                _products = File.Exists(AppPaths.ProductsPath)
                    ? ProductLoader.LoadAllowedProducts(AppPaths.ProductsPath)
                    : new List<Product>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                _products = new List<Product>();
            }

            _matcher = null;
        }

        return _products;
    }

    /// <summary>Turns a typed name ("tomatoes") into a product, tolerant of case, accents and plurals.</summary>
    public static ProductMatcher GetProductMatcher()
    {
        if (_matcher == null)
        {
            _matcher = new ProductMatcher(GetAllowedProducts());
        }

        return _matcher;
    }

    /// <summary>The recipes as last loaded.</summary>
    public static List<Recipe> GetLoadedRecipes()
    {
        if (_recipes == null)
        {
            ReloadRecipes();
        }

        return _recipes;
    }

    /// <summary>Reads Recipes.txt again (for example after a recipe was added).</summary>
    public static List<Recipe> ReloadRecipes()
    {
        _recipes = File.Exists(AppPaths.RecipesPath)
            ? Recipes.LoadRecipesFromTxt(AppPaths.RecipesPath, GetAllowedProducts())
            : new List<Recipe>();

        try
        {
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            var favorites = File.Exists(AppPaths.FavoriteRecipesPath)
                ? File.ReadAllLines(AppPaths.FavoriteRecipesPath).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var recent = File.Exists(AppPaths.RecentlyViewedRecipesPath)
                ? File.ReadAllLines(AppPaths.RecentlyViewedRecipesPath).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipe in _recipes)
            {
                recipe.IsFavorite = favorites.Contains(recipe.RecipeId.ToString());
                recipe.IsRecentlyViewed = recent.Contains(recipe.RecipeId.ToString());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return _recipes;
    }

    /// <summary>Goes to the Recipes screen with this recipe already open.</summary>
    public static void OpenRecipe(Recipe recipe)
    {
        if (Application.Current?.MainWindow is MainWindow window)
            window.OpenRecipe(recipe.RecipeId);
    }
}
