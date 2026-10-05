using System.IO;

namespace NutriTrack.UI;

/// <summary>Locations of the data files and image folder, next to the running application.</summary>
internal static class AppPaths
{
    public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;
    public static string RecipesPath => Path.Combine(BaseDir, "Recipes.txt");
    public static string ProductsPath => Path.Combine(BaseDir, "Products.txt");
    public static string PantryPath => Path.Combine(BaseDir, "Pantry.txt");
    public static string ShoppingListPath => Path.Combine(BaseDir, "ShoppingList.txt");
    public static string ImagesDirectory => Path.Combine(BaseDir, "Assets", "Images");
}
