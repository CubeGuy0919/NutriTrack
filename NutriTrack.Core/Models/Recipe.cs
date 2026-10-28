namespace NutriTrack.Core.Models;

public class Recipe
{
    /// <summary>Servings assumed when a recipe has no servings value on file.</summary>
    public const int DefaultServings = 4;

    public int RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }

    /// <summary>Number of servings the ingredient quantities are written for. 0 = not on file.</summary>
    public int Servings { get; set; }

    public double CaloriesPerServing { get; set; }

    // Optional macros per serving: null = not provided (never invented).
    public double? ProteinPerServing { get; set; }
    public double? CarbsPerServing { get; set; }
    public double? FatPerServing { get; set; }

    public List<string> FoodSensitivities { get; set; } = new();
    public List<IngredientItem> Ingredients { get; set; } = new();
    public List<string> Instructions { get; set; } = new();

    /// <summary>Image file names inside Assets/Images. Empty = the placeholder image is used.</summary>
    public List<string> ImageFileNames { get; set; } = new();

    // ---- Servings and scaling ----

    public bool HasKnownServings => Servings > 0;

    /// <summary>Servings the stored quantities refer to; <see cref="DefaultServings"/> when unknown.</summary>
    public int BaseServings => HasKnownServings ? Servings : DefaultServings;

    /// <summary>selected servings / base servings, e.g. 6 / 4 = 1.5. Ingredient quantities are multiplied by this.</summary>
    public double GetScaleFactor(int selectedServings) =>
        selectedServings <= 0 ? 0 : (double)selectedServings / BaseServings;

    // ---- Text for the recipe cards ----

    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
    public string TimeLabel => $"{TotalTimeMinutes} min";
    public string IngredientCountLabel => $"{Ingredients.Count} ingredients";
    public string CaloriesLabel => $"{CaloriesPerServing:0} kcal";

    /// <summary>The data has no difficulty, so: few ingredients and a short time = Easy.</summary>
    public string Difficulty => (Ingredients.Count <= 7 && TotalTimeMinutes <= 35) ? "Easy" : "Involved";

    public string SensitivitiesLabel =>
        FoodSensitivities.Count == 0 ? "No flagged sensitivities" : string.Join(" · ", FoodSensitivities);
}
