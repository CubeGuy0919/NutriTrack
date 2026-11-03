using System.IO;

namespace NutriTrack.UI;

/// <summary>Where the files live. Everything sits next to the running .exe.</summary>
internal static class AppPaths
{
    private static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

    public static string RecipesPath => Path.Combine(BaseDir, "Data", "Recipes.txt");
    public static string ProductsPath => Path.Combine(BaseDir, "Data", "Products.txt");
    public static string ImagesDirectory => Path.Combine(BaseDir, "Assets", "Images");

    public static string ShoppingListPath => Path.Combine(BaseDir, "ShoppingList.txt");

    public static string UserDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NutriTrack");
    public static string FavoriteRecipesPath => Path.Combine(UserDataDirectory, "favorite-recipes.txt");
    public static string RecentlyViewedRecipesPath => Path.Combine(UserDataDirectory, "recently-viewed-recipes.txt");
    public static string RecipeImageCacheDirectory => Path.Combine(UserDataDirectory, "RecipeImages");
}
