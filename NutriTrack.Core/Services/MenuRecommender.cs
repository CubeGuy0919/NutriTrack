using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>What the user can choose on the Menu screen.</summary>
public class MenuOptions
{
    /// <summary>Calories the whole day should come to.</summary>
    public int TargetCalories { get; set; } = 2000;

    /// <summary>"" = any recipe, otherwise a tag from Recipes.txt: "Vegan", "Gluten-Free" or "Dairy-Free".</summary>
    public string Diet { get; set; } = string.Empty;

    /// <summary>0 = the best menu. 1, 2, 3 ... (the "Suggest another menu" button) pick other good recipes.</summary>
    public int Variation { get; set; }
}

/// <summary>
/// Recommends a menu for one day: breakfast, lunch, dinner and a snack.
///
///   1. For every slot only the matching recipe categories are looked at (see CategoriesFor).
///   2. Every candidate recipe gets a score from 5 simple rules (see ScoreRecipe).
///   3. The best recipe of each slot goes into the menu.
/// Like AnalyticsService this class does not load files and does not know about WPF: it only receives lists.
/// </summary>
public class MenuRecommender
{
    private static readonly MealType[] Slots = { MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack };

    /// <summary>The diet filter choices shown on screen. "Any" means no filter.</summary>
    public static readonly string[] DietChoices = { "Any", "Vegan", "Gluten-Free", "Dairy-Free" };

    /// <summary>Which recipe categories (column "Category" in Recipes.txt) belong to which part of the day.</summary>
    public static string[] CategoriesFor(MealType type) => type switch
    {
        MealType.Breakfast => new[] { "Breakfast", "Bread" },
        MealType.Lunch => new[] { "Lunch", "Soup", "Salad" },
        MealType.Dinner => new[] { "Dinner", "Meal" },
        _ => new[] { "Snack", "Dessert" }
    };

    /// <summary>Share of the daily calories that a slot should provide.</summary>
    public static double CalorieShare(MealType type) => type switch
    {
        MealType.Breakfast => 0.25,
        MealType.Lunch => 0.30,
        MealType.Dinner => 0.35,
        _ => 0.10
    };

    public MenuPlan Recommend(
        IEnumerable<Recipe> recipes,
        IEnumerable<PantryItem> pantry,
        IEnumerable<MealLogEntry> history,
        MenuOptions options)
    {
        var usable = recipes
            .Where(r => r.CaloriesPerServing > 0 && MatchesDiet(r, options.Diet))
            .ToList();

        var pantryItems = pantry.ToList();

        // Products at home. Expired food is NOT counted: we never recommend cooking with it.
        var inStock = pantryItems
            .Where(p => p.ProductId > 0 && p.Quantity > 0 && p.ExpiryStatus != ExpiryStatus.Expired)
            .Select(p => p.ProductId)
            .ToHashSet();

        // Products that should be used up soon (ProductId -> name for the explanation text).
        var useSoon = pantryItems
            .Where(p => p.ProductId > 0 && p.Quantity > 0 && p.ExpiryStatus == ExpiryStatus.ExpiringSoon)
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.First().ProductName);

        // Recipes eaten in the last 7 days (from the meal log) - we prefer something different.
        DateTime weekAgo = DateTime.Today.AddDays(-7);
        var eatenRecently = history
            .Where(h => h.EatenAt >= weekAgo)
            .Select(h => h.RecipeId)
            .ToHashSet();

        var random = new Random(DateTime.Today.DayOfYear * 31 + options.Variation);
        var meals = new List<MenuMeal>();

        foreach (var slot in Slots)
        {
            string[] categories = CategoriesFor(slot);

            var best = usable
                .Where(r => categories.Contains(r.Category, StringComparer.OrdinalIgnoreCase))
                .Select(r => ScoreRecipe(r, slot, options.TargetCalories, inStock, useSoon, eatenRecently))
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Recipe.Name)
                .Take(5)
                .ToList();

            if (best.Count == 0) continue;   // e.g. the diet filter left nothing for this slot

            // Variation 0 = the winner. Otherwise one of the 5 best, so every click gives a good but different menu.
            meals.Add(options.Variation == 0 ? best[0] : best[random.Next(best.Count)]);
        }

        return new MenuPlan { Meals = meals, TargetCalories = options.TargetCalories };
    }

    // The scoring rules (maximum 110 points)

    private static MenuMeal ScoreRecipe(
        Recipe recipe,
        MealType slot,
        int targetCalories,
        HashSet<int> inStock,
        Dictionary<int, string> useSoon,
        HashSet<int> eatenRecently)
    {
        double score = 0;
        var reasons = new List<string>();

        // Rule 1 (max 40): how much of the recipe is already in the pantry.
        int total = recipe.Ingredients.Count;
        int matched = recipe.Ingredients.Count(i => i.ProductId.HasValue && inStock.Contains(i.ProductId.Value));
        if (total > 0) score += 40.0 * matched / total;
        if (matched > 0) reasons.Add($"You already have {matched} of its {total} ingredients");

        // Rule 2 (+20): it uses food that is about to expire.
        var expiring = recipe.Ingredients
            .Where(i => i.ProductId.HasValue && useSoon.ContainsKey(i.ProductId.Value))
            .Select(i => useSoon[i.ProductId!.Value])
            .Distinct()
            .ToList();
        if (expiring.Count > 0)
        {
            score += 20;
            reasons.Add("Uses food that expires soon: " + string.Join(", ", expiring));
        }

        // Rule 3 (max 25): the calories fit the slot. We try 1 or 2 servings and keep whichever is closer.
        double slotTarget = targetCalories * CalorieShare(slot);
        int servings = (int)Math.Clamp(Math.Round(slotTarget / recipe.CaloriesPerServing), 1, 2);
        double kcal = recipe.CaloriesPerServing * servings;
        double fit = 1 - Math.Min(1, Math.Abs(kcal - slotTarget) / slotTarget);
        score += 25 * fit;
        if (fit >= 0.8) reasons.Add($"Fits the calorie budget for {slot.ToString().ToLowerInvariant()} (about {slotTarget:0} kcal)");

        // Rule 4: favorites and quick recipes get a small bonus.
        if (recipe.IsFavorite)
        {
            score += 10;
            reasons.Add("One of your favorite recipes");
        }
        if (recipe.TotalTimeMinutes > 0 && recipe.TotalTimeMinutes <= 30)
        {
            score += 5;
            reasons.Add($"Ready in {recipe.TotalTimeMinutes} minutes");
        }

        // Rule 5: variety. -25 when eaten in the last 7 days, +10 when not.
        if (eatenRecently.Contains(recipe.RecipeId))
        {
            score -= 25;
        }
        else
        {
            score += 10;
            reasons.Add("You have not eaten it in the last 7 days");
        }

        return new MenuMeal
        {
            Slot = slot,
            Recipe = recipe,
            Servings = servings,
            Score = score,
            PantryMatched = matched,
            PantryTotal = total,
            Reasons = reasons.Take(4).ToList()
        };
    }

    private static bool MatchesDiet(Recipe recipe, string diet) =>
        string.IsNullOrWhiteSpace(diet) ||
        diet.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
        recipe.FoodSensitivities.Contains(diet, StringComparer.OrdinalIgnoreCase);
}
