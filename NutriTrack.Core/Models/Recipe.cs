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
    public List<string> Ingredients { get; set; } = new();
    public string Instructions { get; set; } = string.Empty;
    // Sensitivity 
}