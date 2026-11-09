
namespace NutriTrack.Core.Models;

/// <summary>The four parts of a day that a menu is built from.</summary>
public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

/// <summary>
/// One thing the user ate ("I ate this"). It is saved in MealLog.txt and is the data behind the "Eating habits"
/// part of the Analytics screen.
/// The nutrition values are a SNAPSHOT (per-serving value x servings) taken at the moment of logging, so old entries
/// stay correct even if Recipes.txt is edited later.
/// </summary>
public class MealLogEntry
{
    public int EntryId { get; set; }

    public int RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public MealType MealType { get; set; }

    public DateTime EatenAt { get; set; } = DateTime.Now;
    public int Servings { get; set; } = 1;

    /// <summary>Total calories of this entry (calories per serving x servings).</summary>
    public double Calories { get; set; }

    // Only about 1 recipe in 5 has macro values in Recipes.txt, so these can be null ("unknown", never 0).
    public double? Protein { get; set; }
    public double? Carbs { get; set; }
    public double? Fat { get; set; }

    public bool HasMacros => Protein.HasValue && Carbs.HasValue && Fat.HasValue;
}
