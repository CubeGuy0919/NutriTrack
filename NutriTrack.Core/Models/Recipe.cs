using Microsoft.VisualBasic;
using NutriTrack.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NutriTrack.Core.Models;

public class IngredientItem
{
    public double Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Recipe
{
    private int recipeId { get; }
    private string name { get; } = string.Empty;
    private string cuisine { get; } = string.Empty;
    private string category { get; } = string.Empty;
    private int prepTimeMinutes { get; }
    private int cookTimeMinutes { get; }
    private double caloriesPerServing { get; }

    private int servings { get; }

    // Processed collections
    private List<string> foodSensitivities { get; } = new();
    private List<string> ingredients { get; } = new();
    private List<string> instructions { get; } = new();

    private List<string> imageFileNames { get; }

    public int RecipeId => recipeId;
    public string Name => name;
    public string Cuisine => cuisine;
    public string Category => category;
    public int PrepTimeMinutes => prepTimeMinutes;
    public int CookTimeMinutes => cookTimeMinutes;
    public double CaloriesPerServing => caloriesPerServing;

    public int Servings => servings;

    // Processed collections
    public List<string> FoodSensitivities => foodSensitivities;
    public List<string> Ingredients => ingredients;
    public List<string> Instructions => instructions;

    public List<string> ImageFileNames => imageFileNames;

    // Formatted helper properties for DataGrid/ListView bindings
    public string FormattedSensitivities => string.Join(" \n ", FoodSensitivities.Prepend(string.Empty));
    public string FormattedIngredients => string.Join(" \n ", Ingredients.Prepend(string.Empty));
    public string FormattedInstructions => string.Join(" \n", Instructions.Prepend(string.Empty));

    public Recipe(int recipeId, string name, string cuisine, string category, int prepTimeMinutes, int cookTimeMinutes, double caloriesPerServing, int servings, List<string> foodSensitivities, List<string> ingredients, List<string> instructions, List<string> imageFileNames)
    {
        this.recipeId = recipeId;
        this.name = name;
        this.cuisine = cuisine;
        this.category = category;
        this.prepTimeMinutes = prepTimeMinutes;
        this.cookTimeMinutes = cookTimeMinutes;
        this.caloriesPerServing = caloriesPerServing;
        this.servings = servings;
        this.foodSensitivities = foodSensitivities;
        this.ingredients = ingredients;
        this.instructions = instructions;
        this.imageFileNames = imageFileNames;
    }
    public string GetScaledAndConvertedIngredients(double scaleFactor, bool useImperial)
    {
        if (Ingredients == null || Ingredients.Count == 0)
            return "No ingredients specified.";

        var sb = new StringBuilder();

        foreach (var ingredientLine in Ingredients)
        {
            var parsed = ParseIngredientLine(ingredientLine);

            if (parsed == null)
            {
                sb.AppendLine($"• {ingredientLine}");
                continue;
            }

            double scaledQuantity = parsed.Quantity * scaleFactor;
            string displayUnit = parsed.Unit;

            if (useImperial && !string.IsNullOrWhiteSpace(displayUnit))
            {
                (scaledQuantity, displayUnit) = ConvertMetricToImperial(scaledQuantity, displayUnit);
            }

            string formattedQuantity = FormatQuantity(scaledQuantity);
            if (string.IsNullOrWhiteSpace(displayUnit))
            {
                sb.AppendLine($"• {formattedQuantity} {parsed.Name}".Trim());
            }
            else
            {
                sb.AppendLine($"• {formattedQuantity} {displayUnit} {parsed.Name}".Trim());
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static IngredientItem? ParseIngredientLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var match = Regex.Match(line.Trim(), @"^(?<qty>\d+(?:[\.,]\d+)?(?:\/\d+)?)\s*(?<unit>g|kg|ml|l|tsp|tbsp|cup|cups|oz|lb)?\s*(?<name>.*)$", RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        string qtyStr = match.Groups["qty"].Value;
        double quantity;

        if (qtyStr.Contains('/'))
        {
            var parts = qtyStr.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0], out double num) && double.TryParse(parts[1], out double den) && den != 0)
            {
                quantity = num / den;
            }
            else return null;
        }
        else if (!double.TryParse(qtyStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out quantity))
        {
            return null;
        }

        return new IngredientItem
        {
            Quantity = quantity,
            Unit = match.Groups["unit"].Value.ToLowerInvariant(),
            Name = match.Groups["name"].Value.Trim()
        };
    }

    private static (double Quantity, string Unit) ConvertMetricToImperial(double qty, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "g" when qty >= 453.592 => (qty / 453.592, "lb"),
            "g" => (qty / 28.3495, "oz"),
            "kg" => (qty * 2.20462, "lb"),
            "ml" when qty >= 240 => (qty / 240.0, "cups"),
            "ml" => (qty / 29.5735, "fl oz"),
            "l" => (qty * 4.22675, "cups"),
            _ => (qty, unit)
        };
    }

    private static string FormatQuantity(double qty)
    {
        if (qty <= 0) return "0";
        if (Math.Abs(qty % 1) < 0.01) return ((int)qty).ToString();
        return qty.ToString("0.##", CultureInfo.InvariantCulture);
    }
}