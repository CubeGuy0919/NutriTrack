namespace NutriTrack.Core.Models;

/// <summary>How well the current pantry covers one recipe (used by the Pantry and Dashboard screens).</summary>
public class RecipeMatchResult
{
    public Recipe Recipe { get; set; } = new();
    public int MatchedIngredientsCount { get; set; }
    public int TotalIngredientsCount { get; set; }

    /// <summary>Display text of the ingredients the pantry covers.</summary>
    public List<string> MatchedIngredients { get; set; } = new();

    /// <summary>Display text of the ingredients the pantry does not cover.</summary>
    public List<string> MissingIngredients { get; set; } = new();

    public double MatchPercentage => TotalIngredientsCount > 0
        ? (double)MatchedIngredientsCount / TotalIngredientsCount
        : 0;

    public string MatchBadge
    {
        get
        {
            if (MatchedIngredientsCount >= TotalIngredientsCount && TotalIngredientsCount > 0)
                return "✓ All ingredients available";
            if (MatchPercentage >= 0.5)
                return "✓ Most ingredients available";
            return $"{MatchedIngredientsCount}/{TotalIngredientsCount} ingredients";
        }
    }
}
