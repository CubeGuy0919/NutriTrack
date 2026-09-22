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

    // ---- Planned and Detail Features ----
    public int Servings { get; set; } = 4;
    public List<string> ImageFileNames { get; set; } = new();
    public bool IsFavorite { get; set; } = false;
    public int CookedCount { get; set; } = 0;

    public string GetScaledAndConvertedIngredients(double scaleFactor, bool useImperial = false)
    {
        if (scaleFactor <= 0) scaleFactor = 1.0;
        var scaled = new List<string>();

        foreach (var ingredient in Ingredients)
        {
            if (string.IsNullOrWhiteSpace(ingredient)) continue;

            // Attempt to scale leading numeric portion
            var match = System.Text.RegularExpressions.Regex.Match(ingredient.Trim(), @"^(\d+(?:[.,]\d+)?|\d+/\d+)\s*(.*)$");
            if (match.Success)
            {
                string numStr = match.Groups[1].Value.Replace(',', '.');
                string rest = match.Groups[2].Value;
                double amount;

                if (numStr.Contains('/'))
                {
                    var frac = numStr.Split('/');
                    if (frac.Length == 2 && double.TryParse(frac[0], out double num) && double.TryParse(frac[1], out double den) && den != 0)
                        amount = num / den;
                    else
                        amount = 1.0;
                }
                else if (!double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out amount))
                {
                    amount = 1.0;
                }

                double scaledAmount = amount * scaleFactor;

                if (useImperial)
                {
                    // Convert metric units to imperial if applicable
                    if (rest.StartsWith("g ", StringComparison.OrdinalIgnoreCase))
                    {
                        double oz = scaledAmount * 0.035274;
                        rest = "oz " + rest[2..].TrimStart();
                        scaled.Add($"{oz:0.#} {rest}");
                        continue;
                    }
                    else if (rest.StartsWith("ml ", StringComparison.OrdinalIgnoreCase))
                    {
                        double flOz = scaledAmount * 0.033814;
                        rest = "fl oz " + rest[3..].TrimStart();
                        scaled.Add($"{flOz:0.#} {rest}");
                        continue;
                    }
                }

                string formattedAmount = scaledAmount >= 10 ? $"{scaledAmount:0}" : $"{scaledAmount:0.#}";
                scaled.Add($"{formattedAmount} {rest}");
            }
            else
            {
                scaled.Add(ingredient);
            }
        }

        return string.Join(" \n ", scaled.Prepend(string.Empty));
    }
}