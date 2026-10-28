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

    private static readonly ProductService ProductLoader = new();
    private static List<Product>? _products;
    private static ProductMatcher? _matcher;
    private static List<Recipe>? _recipes;

    /// <summary>The product list (Products.txt). The only source of ingredients that recipes, pantry and receipts accept.</summary>
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
                _products = new List<Product>();   // unreadable file: the app starts with an empty product list
            }

            _matcher = null;
        }

        return _products;
    }

    /// <summary>Turns a typed name ("tomatoes") into a product, tolerant of case, accents and plurals.</summary>
    public static ProductMatcher GetProductMatcher() => _matcher ??= new ProductMatcher(GetAllowedProducts());

    /// <summary>The recipes as last loaded.</summary>
    public static List<Recipe> GetLoadedRecipes() => _recipes ?? ReloadRecipes();

    /// <summary>Reads Recipes.txt again (for example after a recipe was added).</summary>
    public static List<Recipe> ReloadRecipes()
    {
        _recipes = File.Exists(AppPaths.RecipesPath)
            ? Recipes.LoadRecipesFromTxt(AppPaths.RecipesPath, GetAllowedProducts())
            : new List<Recipe>();

        return _recipes;
    }

    /// <summary>Goes to the Recipes screen with this recipe already open.</summary>
    public static void OpenRecipe(Recipe recipe)
    {
        if (Application.Current?.MainWindow is MainWindow window)
            window.OpenRecipe(recipe.RecipeId);
    }
}
