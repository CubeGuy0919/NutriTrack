using System;
using System.Collections.Generic;
using System.IO;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI;

/// <summary>
/// Central shared service instances acting as the single source of truth for all views.
/// </summary>
public static class AppServices
{
    public static ProductService Products { get; } = new();
    public static RecipeService Recipes { get; } = new();
    public static ReceiptService Receipts { get; } = new();
    public static PantryService Pantry { get; } = new();
    public static NutritionAnalyticsService Analytics { get; } = new();

    private static List<Product>? _cachedProducts;
    private static List<Recipe>? _cachedRecipes;

    public static List<Product> GetAllowedProducts()
    {
        if (_cachedProducts == null || _cachedProducts.Count == 0)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Products.txt");
            _cachedProducts = Products.LoadAllowedProducts(path);
        }
        return _cachedProducts;
    }

    public static List<Recipe> GetLoadedRecipes()
    {
        if (_cachedRecipes == null || _cachedRecipes.Count == 0)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipes.txt");
            _cachedRecipes = Recipes.LoadRecipesFromTxt(path);
        }
        return _cachedRecipes;
    }
}
