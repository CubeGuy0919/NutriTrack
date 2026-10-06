using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Loads, validates and saves recipes.
///
/// Recipes.txt is read by column NAME (from its header line), so both the original 10-column v4 file
/// and the extended file written by this service load correctly. Saving always writes the extended header.
/// The Instructions column is kept LAST, so instruction text may still contain commas.
/// </summary>
public class RecipeService
{
    public const string FileHeader =
        "RecipeId,Name,Cuisine,Category,Description,PrepTimeMinutes,CookTimeMinutes,Servings,CaloriesPerServing," +
        "ProteinPerServing,CarbsPerServing,FatPerServing,FoodSensitivities,Ingredients,ImageFilename,Instructions";

    private static readonly string[] LegacyColumns =
    {
        "RecipeId", "Name", "Cuisine", "Category", "PrepTimeMinutes", "CookTimeMinutes",
        "CaloriesPerServing", "FoodSensitivities", "Ingredients", "Instructions"
    };

    private readonly RecipeImageService _imageService;

    public RecipeService() : this(new RecipeImageService()) { }

    public RecipeService(RecipeImageService imageService)
    {
        _imageService = imageService;
    }

    // ---------------------------------------------------------------
    // Load
    // ---------------------------------------------------------------

    /// <summary>
    /// Loads recipes. When <paramref name="products"/> is supplied, each ingredient is linked to its
    /// Product (ProductId) where the match is confident; unmatched legacy ingredients keep ProductId = null.
    /// </summary>
    public List<Recipe> LoadRecipesFromTxt(string filePath, IReadOnlyList<Product>? products = null)
    {
        var recipes = new List<Recipe>();

        if (!File.Exists(filePath))
            return recipes;

        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length == 0) return recipes;

        string[] columns = lines[0].TrimStart('\uFEFF').Split(',').Select(c => c.Trim()).ToArray();
        if (!columns.Contains("Name", StringComparer.OrdinalIgnoreCase))
            columns = LegacyColumns; // header missing/unknown: assume the original v4 layout

        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Length; i++)
            index[columns[i]] = i;

        // v3 files used "ImageFilename"; accept the plural spelling as well
        if (!index.ContainsKey("ImageFilename") && index.TryGetValue("ImageFileNames", out int imgIdx))
            index["ImageFilename"] = imgIdx;

        var matcher = products is { Count: > 0 } ? new ProductMatcher(products) : null;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // The last column absorbs any extra commas (instruction text)
            var parts = line.Split(',', columns.Length);
            if (parts.Length < columns.Length) continue;

            try
            {
                string Get(string column) =>
                    index.TryGetValue(column, out int i) && i < parts.Length ? parts[i].Trim() : string.Empty;

                var recipe = new Recipe
                {
                    RecipeId = int.TryParse(Get("RecipeId"), out int id) ? id : 0,
                    Name = Unescape(Get("Name")),
                    Cuisine = Unescape(Get("Cuisine")),
                    Category = Unescape(Get("Category")),
                    Description = Unescape(Get("Description")),
                    PrepTimeMinutes = int.TryParse(Get("PrepTimeMinutes"), out int prep) ? prep : 0,
                    CookTimeMinutes = int.TryParse(Get("CookTimeMinutes"), out int cook) ? cook : 0,
                    Servings = int.TryParse(Get("Servings"), out int servings) && servings > 0 ? servings : 0,
                    CaloriesPerServing = ParseDouble(Get("CaloriesPerServing")) ?? 0,
                    ProteinPerServing = ParseDouble(Get("ProteinPerServing")),
                    CarbsPerServing = ParseDouble(Get("CarbsPerServing")),
                    FatPerServing = ParseDouble(Get("FatPerServing")),
                    FoodSensitivities = Get("FoodSensitivities").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    Instructions = Get("Instructions").Split('|', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    ImageFileNames = Get("ImageFilename").Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                };

                foreach (var text in Get("Ingredients").Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var ingredient = IngredientParser.Parse(text);
                    if (matcher != null && ingredient.IsParsed)
                        ingredient.ProductId = matcher.Match(ingredient.Name)?.ProductId;
                    recipe.Ingredients.Add(ingredient);
                }

                recipes.Add(recipe);
            }
            catch
            {
                // Skip corrupted individual lines
                continue;
            }
        }

        return recipes;
    }

    // ---------------------------------------------------------------
    // Save
    // ---------------------------------------------------------------

    /// <summary>
    /// Writes the whole recipe list in the extended format. The write goes to a temp file first and the previous
    /// file is kept as Recipes.txt.bak, so a crash can never leave a half-written Recipes.txt.
    /// </summary>
    public void SaveRecipesToTxt(string filePath, IEnumerable<Recipe> recipes)
    {
        var sb = new StringBuilder();
        sb.Append(FileHeader).Append("\r\n");

        foreach (var r in recipes)
        {
            var fields = new[]
            {
                r.RecipeId.ToString(CultureInfo.InvariantCulture),
                Escape(r.Name),
                Escape(r.Cuisine),
                Escape(r.Category),
                Escape(r.Description),
                r.PrepTimeMinutes.ToString(CultureInfo.InvariantCulture),
                r.CookTimeMinutes.ToString(CultureInfo.InvariantCulture),
                r.Servings > 0 ? r.Servings.ToString(CultureInfo.InvariantCulture) : string.Empty,
                FormatDouble(r.CaloriesPerServing),
                r.ProteinPerServing.HasValue ? FormatDouble(r.ProteinPerServing.Value) : string.Empty,
                r.CarbsPerServing.HasValue ? FormatDouble(r.CarbsPerServing.Value) : string.Empty,
                r.FatPerServing.HasValue ? FormatDouble(r.FatPerServing.Value) : string.Empty,
                string.Join(';', r.FoodSensitivities.Select(s => Sanitize(s, ';'))),
                string.Join(';', r.Ingredients.Select(i => Sanitize(i.ToStorageString(), ';'))),
                string.Join('|', r.ImageFileNames.Select(s => Sanitize(s, '|'))),
                string.Join('|', r.Instructions.Select(s => Sanitize(s, '|', keepCommas: true)))
            };
            sb.Append(string.Join(',', fields)).Append("\r\n");
        }

        string tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, sb.ToString(), new UTF8Encoding(false));

        if (File.Exists(filePath))
            File.Copy(filePath, filePath + ".bak", overwrite: true);

        File.Move(tempPath, filePath, overwrite: true);
    }

    // ---------------------------------------------------------------
    // Validate
    // ---------------------------------------------------------------

    /// <summary>Checks a NEW recipe without changing it or saving anything.</summary>
    public RecipeValidationResult Validate(Recipe recipe, IEnumerable<Recipe> existingRecipes, IReadOnlyList<Product> products)
    {
        var result = new RecipeValidationResult();
        var matcher = new ProductMatcher(products);

        // --- basic fields ---
        string name = recipe.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            result.Errors.Add("Please enter a recipe name.");
        else
        {
            string key = ProductMatcher.Normalize(name);
            if (existingRecipes.Any(r => ProductMatcher.Normalize(r.Name) == key))
            {
                result.DuplicateName = true;
                result.Errors.Add("A recipe with this name already exists.");
            }
        }

        if (string.IsNullOrWhiteSpace(recipe.Category)) result.Errors.Add("Please enter a category.");
        if (string.IsNullOrWhiteSpace(recipe.Cuisine)) result.Errors.Add("Please enter a cuisine.");
        if (recipe.PrepTimeMinutes < 0 || recipe.CookTimeMinutes < 0) result.Errors.Add("Preparation and cooking time cannot be negative.");
        if (recipe.Servings < 1) result.Errors.Add("Base servings must be at least 1.");
        if (recipe.CaloriesPerServing < 0) result.Errors.Add("Calories per serving cannot be negative.");
        if (recipe.ProteinPerServing < 0 || recipe.CarbsPerServing < 0 || recipe.FatPerServing < 0)
            result.Errors.Add("Protein, carbohydrates and fat cannot be negative.");
        if (recipe.Instructions.Count(s => !string.IsNullOrWhiteSpace(s)) == 0)
            result.Errors.Add("Please enter at least one instruction step.");

        // --- ingredients ---
        if (recipe.Ingredients.Count == 0)
            result.Errors.Add("Please add at least one ingredient.");

        foreach (var ingredient in recipe.Ingredients)
        {
            string label = string.IsNullOrWhiteSpace(ingredient.Name) ? "(unnamed ingredient)" : ingredient.Name.Trim();

            if (ResolveProduct(ingredient, matcher) == null)
            {
                result.InvalidIngredients.Add(new InvalidIngredient
                {
                    Name = label,
                    Suggestions = matcher.Suggest(ingredient.Name).Select(p => p.Name).ToList()
                });
                continue;
            }

            if (!(ingredient.Quantity > 0))
                result.Errors.Add($"Ingredient \"{label}\" needs a quantity greater than 0.");
        }

        return result;
    }

    // ---------------------------------------------------------------
    // Add
    // ---------------------------------------------------------------

    /// <summary>
    /// Validates the recipe, optionally imports its image, assigns a new RecipeId, writes Recipes.txt and only then
    /// adds the recipe to <paramref name="existingRecipes"/>. On any problem nothing is added and the result explains why.
    /// </summary>
    public RecipeValidationResult AddRecipe(
        string recipesFilePath,
        Recipe recipe,
        List<Recipe> existingRecipes,
        IReadOnlyList<Product> products,
        string? imageSourcePath = null,
        string? imagesDirectory = null)
    {
        var result = Validate(recipe, existingRecipes, products);
        if (!result.IsValid) return result;

        // Clean up text and link every ingredient to its product (canonical product name)
        var matcher = new ProductMatcher(products);
        recipe.Name = recipe.Name.Trim();
        recipe.Cuisine = recipe.Cuisine.Trim();
        recipe.Category = recipe.Category.Trim();
        recipe.Description = recipe.Description?.Trim() ?? string.Empty;
        recipe.Instructions = recipe.Instructions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        recipe.FoodSensitivities = recipe.FoodSensitivities.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

        foreach (var ingredient in recipe.Ingredients)
        {
            var product = ResolveProduct(ingredient, matcher)!;
            ingredient.ProductId = product.ProductId;
            ingredient.Name = product.Name;
        }

        recipe.RecipeId = existingRecipes.Count == 0 ? 1 : existingRecipes.Max(r => r.RecipeId) + 1;

        // Optional image
        string? importedImage = null;
        if (!string.IsNullOrWhiteSpace(imageSourcePath) && !string.IsNullOrWhiteSpace(imagesDirectory))
        {
            try
            {
                importedImage = _imageService.ImportImage(imageSourcePath, imagesDirectory, recipe.Name);
                recipe.ImageFileNames = new List<string> { importedImage };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                result.Errors.Add($"The image could not be saved: {ex.Message}");
                return result;
            }
        }

        // Persist, then update the in-memory collection
        try
        {
            SaveRecipesToTxt(recipesFilePath, existingRecipes.Concat(new[] { recipe }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (importedImage != null && imagesDirectory != null)
                _imageService.TryDelete(imagesDirectory, importedImage);

            result.Errors.Add($"The recipe could not be written to Recipes.txt: {ex.Message}");
            return result;
        }

        existingRecipes.Add(recipe);
        result.SavedRecipe = recipe;
        return result;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>Product for an ingredient: by ProductId if given (and still in the list), otherwise by exact normalized name.</summary>
    private static Product? ResolveProduct(IngredientItem ingredient, ProductMatcher matcher)
    {
        if (ingredient.ProductId.HasValue)
            return matcher.GetById(ingredient.ProductId.Value);

        return matcher.Match(ingredient.Name);
    }

    private static double? ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;

    private static string FormatDouble(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Commas would break the CSV layout, so free-text fields store them as %2C (and a literal % as %25).</summary>
    private static string Escape(string? text) =>
        (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("%", "%25").Replace(",", "%2C");

    private static string Unescape(string text) =>
        text.Replace("%2C", ",").Replace("%25", "%");

    /// <summary>
    /// Removes characters that would break the row: line breaks, the list separator itself, and commas
    /// (except in the last column, Instructions, which absorbs extra commas when loading).
    /// </summary>
    private static string Sanitize(string? text, char listSeparator, bool keepCommas = false)
    {
        string cleaned = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        cleaned = cleaned.Replace(listSeparator, listSeparator == '|' ? '/' : ' ');
        if (!keepCommas) cleaned = cleaned.Replace(',', ' ');
        return cleaned;
    }
}
