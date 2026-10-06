namespace NutriTrack.Core.Models;

public class Recipe
{
    /// <summary>Base servings assumed when a recipe has no servings value on file (older Recipes.txt rows).</summary>
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

    // Processed collections
    public List<string> FoodSensitivities { get; set; } = new();
    public List<IngredientItem> Ingredients { get; set; } = new();
    public List<string> Instructions { get; set; } = new();

    /// <summary>Explicit image file names inside the application's Assets/Images folder. Empty = use the placeholder.</summary>
    public List<string> ImageFileNames { get; set; } = new();

    // ---- Serving / scaling support (the slider itself comes in Part 2) ----

    public bool HasKnownServings => Servings > 0;

    /// <summary>Servings the stored quantities refer to; falls back to <see cref="DefaultServings"/> when unknown.</summary>
    public int BaseServings => Servings > 0 ? Servings : DefaultServings;

    /// <summary>selected servings / base servings, e.g. 6 / 4 = 1.5. Multiply ingredient and nutrition values by this.</summary>
    public double GetScaleFactor(int selectedServings) =>
        selectedServings <= 0 ? 0 : (double)selectedServings / BaseServings;

    // Formatted helper properties for DataGrid/ListView bindings
    public string FormattedSensitivities => string.Join(" \n ", FoodSensitivities.Prepend(string.Empty));
    public string FormattedIngredients => string.Join(" \n ", Ingredients.Select(i => i.DisplayText).Prepend(string.Empty));
    public string FormattedInstructions => string.Join(" \n", Instructions.Prepend(string.Empty));

    // ---- Card / filter-view helpers ----
    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
    public string TimeLabel => $"{TotalTimeMinutes} min";
    public string IngredientCountLabel => $"{Ingredients.Count} ingredients";
    public string CaloriesLabel => $"{CaloriesPerServing:0} kcal";

    /// <summary>Cheap heuristic difficulty, since the source data has none.</summary>
    public string Difficulty => (Ingredients.Count <= 7 && TotalTimeMinutes <= 35) ? "Easy" : "Involved";

    public string SensitivitiesLabel =>
        FoodSensitivities.Count == 0 ? "No flagged sensitivities" : string.Join(" · ", FoodSensitivities);

    /// <summary>"Protein 30 g · Carbs 40 g · Fat 10 g" for whichever values exist; empty when none do.</summary>
    public string MacrosLabel
    {
        get
        {
            var parts = new List<string>();
            if (ProteinPerServing.HasValue) parts.Add($"Protein {ProteinPerServing.Value:0.#} g");
            if (CarbsPerServing.HasValue) parts.Add($"Carbs {CarbsPerServing.Value:0.#} g");
            if (FatPerServing.HasValue) parts.Add($"Fat {FatPerServing.Value:0.#} g");
            return string.Join(" · ", parts);
        }
    }
}
