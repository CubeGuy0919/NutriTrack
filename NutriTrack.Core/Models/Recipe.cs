namespace NutriTrack.Core.Models;

public class Recipe
{
    public int RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public double CaloriesPerServing { get; set; }

    // Processed collections
    public List<string> FoodSensitivities { get; set; } = new();
    public List<string> Ingredients { get; set; } = new();
    public List<string> Instructions { get; set; } = new();

    // Formatted helper properties for DataGrid/ListView bindings
    public string FormattedSensitivities => string.Join(" \n ", FoodSensitivities.Prepend(string.Empty));
    public string FormattedIngredients => string.Join(" \n ", Ingredients.Prepend(string.Empty));
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
}