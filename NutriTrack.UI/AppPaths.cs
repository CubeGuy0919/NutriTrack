using System.IO;

namespace NutriTrack.UI;

/// <summary>Where the files live. Everything sits next to the running .exe.</summary>
internal static class AppPaths
{
    private static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

    // shipped with the app (the Data folder of the UI project)
    public static string RecipesPath => Path.Combine(BaseDir, "Data", "Recipes.txt");
    public static string ProductsPath => Path.Combine(BaseDir, "Data", "Products.txt");
    public static string ImagesDirectory => Path.Combine(BaseDir, "Assets", "Images");

    // created while the app runs (Pantry.json and Receipts.json are handled by their own services)
    public static string ShoppingListPath => Path.Combine(BaseDir, "ShoppingList.txt");
}
