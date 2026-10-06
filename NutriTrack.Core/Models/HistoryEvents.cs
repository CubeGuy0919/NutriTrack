namespace NutriTrack.Core.Models;

/// <summary>
/// One thing the user ate. Nutrition is stored as a snapshot of the totals at logging time,
/// so later edits to Products.txt never rewrite history.
/// </summary>
public class ConsumptionEvent
{
    public int EventId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Category { get; set; }

    public double Quantity { get; set; }
    public string Unit { get; set; } = "g";

    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }
    public double Salt { get; set; }

    /// <summary>Set when the food came from cooking a recipe.</summary>
    public int? RecipeId { get; set; }
    public string? RecipeName { get; set; }

    /// <summary>Free text such as Breakfast / Lunch / Dinner / Snack; null when not given.</summary>
    public string? Meal { get; set; }
}

public class PantryDeduction
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>One "I cooked this recipe" event, with the facts needed to rebuild statistics later.</summary>
public class CookingEvent
{
    public int EventId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public int TotalTimeMinutes { get; set; }

    public int Servings { get; set; }

    // Snapshot for the servings that were cooked (null = recipe had no value on file; never invented).
    public double Calories { get; set; }
    public double? Protein { get; set; }
    public double? Carbs { get; set; }
    public double? Fat { get; set; }

    public bool NutritionLogged { get; set; }
    public List<PantryDeduction> PantryDeductions { get; set; } = new();

    /// <summary>Ingredients that could not be matched safely to pantry stock and were left untouched.</summary>
    public List<string> UnmatchedIngredients { get; set; } = new();
}

public enum WasteReason
{
    Expired,
    Discarded
}

/// <summary>Food that left the pantry without being eaten.</summary>
public class WasteEvent
{
    public int EventId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public WasteReason Reason { get; set; }

    /// <summary>Only set when a price is genuinely known (for example from a receipt); null otherwise.</summary>
    public decimal? EstimatedValue { get; set; }
}

/// <summary>Everything persisted in History.json.</summary>
public class HistoryData
{
    public List<ConsumptionEvent> Consumption { get; set; } = new();
    public List<CookingEvent> Cooking { get; set; } = new();
    public List<WasteEvent> Waste { get; set; } = new();
}
