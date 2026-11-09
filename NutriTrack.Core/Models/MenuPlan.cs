namespace NutriTrack.Core.Models;

/// <summary>One recommended meal of the menu (for example "Lunch: Classic Tomato Basil Soup, 2 servings").</summary>
public class MenuMeal
{
    public MealType Slot { get; init; }
    public required Recipe Recipe { get; init; }

    /// <summary>How many servings are recommended so the meal fits the calorie budget of its slot.</summary>
    public int Servings { get; init; } = 1;

    /// <summary>Recommendation score, 0 to about 110. Only used to rank recipes against each other.</summary>
    public double Score { get; init; }

    public int PantryMatched { get; init; }
    public int PantryTotal { get; init; }

    /// <summary>Short sentences that explain WHY this recipe was chosen.</summary>
    public List<string> Reasons { get; init; } = new();

    // ---- numbers for the chosen number of servings ----
    public double Calories => Recipe.CaloriesPerServing * Servings;
    public double? Protein => Recipe.ProteinPerServing * Servings;
    public double? Carbs => Recipe.CarbsPerServing * Servings;
    public double? Fat => Recipe.FatPerServing * Servings;

    // ---- text for the cards ----
    public string SlotLabel => Slot switch
    {
        MealType.Breakfast => "🌅  BREAKFAST",
        MealType.Lunch => "🥗  LUNCH",
        MealType.Dinner => "🍽  DINNER",
        _ => "🍎  SNACK"
    };

    public string Subtitle => $"{Recipe.Cuisine} · {Recipe.Category} · {Recipe.TimeLabel}";
    public string PortionLabel => $"{(Servings == 1 ? "1 serving" : $"{Servings} servings")}  ·  {Calories:0} kcal";
    public string CaloriesLabel => $"{Calories:0} kcal";

    public string PantryLabel => PantryTotal > 0
        ? $"🥫 {PantryMatched} of {PantryTotal} ingredients in your pantry"
        : string.Empty;

    public string MacrosLabel => Recipe.HasMacros
        ? $"P {Protein:0.#}g  ·  C {Carbs:0.#}g  ·  F {Fat:0.#}g"
        : "Macros not available for this recipe";

    public string ReasonsText => string.Join(Environment.NewLine, Reasons.Select(r => "•  " + r));
}

/// <summary>A whole day: one recommended meal per slot, plus the totals.</summary>
public class MenuPlan
{
    public List<MenuMeal> Meals { get; init; } = new();
    public int TargetCalories { get; init; }

    public double TotalCalories => Meals.Sum(m => m.Calories);

    /// <summary>For the progress bar: total / target, 0 to 100.</summary>
    public double CaloriePercent => TargetCalories > 0 ? Math.Min(100, TotalCalories / TargetCalories * 100) : 0;

    public string CaloriesText => $"{TotalCalories:0} of {TargetCalories} kcal";

    public int MealsWithMacros => Meals.Count(m => m.Recipe.HasMacros);

    public string MacrosText
    {
        get
        {
            if (MealsWithMacros == 0) return "Macros: no data for these recipes";

            double protein = Meals.Where(m => m.Recipe.HasMacros).Sum(m => m.Protein ?? 0);
            double carbs = Meals.Where(m => m.Recipe.HasMacros).Sum(m => m.Carbs ?? 0);
            double fat = Meals.Where(m => m.Recipe.HasMacros).Sum(m => m.Fat ?? 0);

            string note = MealsWithMacros < Meals.Count ? $"  (only {MealsWithMacros} of {Meals.Count} meals have macro data)" : string.Empty;
            return $"Protein {protein:0}g  ·  Carbs {carbs:0}g  ·  Fat {fat:0}g{note}";
        }
    }
}
