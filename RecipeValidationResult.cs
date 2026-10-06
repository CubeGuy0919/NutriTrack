namespace NutriTrack.Core.Models;

/// <summary>Outcome of validating (and possibly saving) a recipe. Nothing is ever silently dropped.</summary>
public class RecipeValidationResult
{
    /// <summary>General problems: missing name, duplicate name, no ingredients, save failure...</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Ingredients that are not in the product list, with optional "did you mean" candidates.</summary>
    public List<InvalidIngredient> InvalidIngredients { get; } = new();

    public bool DuplicateName { get; set; }

    public bool IsValid => Errors.Count == 0 && InvalidIngredients.Count == 0;

    /// <summary>Set after a successful save.</summary>
    public Recipe? SavedRecipe { get; set; }

    /// <summary>Message for the user, matching the wording requested in the specification.</summary>
    public string ToUserMessage()
    {
        var lines = new List<string> { "Cannot save recipe." };

        if (Errors.Count > 0)
        {
            lines.Add(string.Empty);
            foreach (var error in Errors) lines.Add($"• {error}");
        }

        if (InvalidIngredients.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("The following ingredients are not available in the product list:");
            lines.Add(string.Empty);
            foreach (var bad in InvalidIngredients)
            {
                string hint = bad.Suggestions.Count > 0
                    ? $"  (did you mean: {string.Join(", ", bad.Suggestions)}?)"
                    : string.Empty;
                lines.Add($"• {bad.Name}{hint}");
            }
            lines.Add(string.Empty);
            lines.Add("Please select valid ingredients.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public class InvalidIngredient
{
    public string Name { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new();
}
