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
    public string FormattedSensitivities => string.Join(", ", FoodSensitivities);
    public string FormattedIngredients => string.Join("\n ", Ingredients.Prepend(string.Empty)).TrimStart();
    public string FormattedInstructions => string.Join("\n", Instructions);
}