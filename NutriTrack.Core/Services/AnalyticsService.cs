using System.Globalization;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// All the numbers for the Analytics screen (money, pantry, recipes and - new - what the user really eats).
/// </summary>
public class AnalyticsService
{

    /// <summary>Keeps only receipts from the last N months (this month counts as month 1).</summary>
    public List<Receipt> FilterByMonths(List<Receipt> receipts, int months)
    {
        DateTime firstOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime startDate = firstOfThisMonth.AddMonths(-(months - 1));

        return receipts.Where(r => r.PurchaseDate >= startDate).ToList();
    }

    public decimal GetTotalSpent(List<Receipt> receipts) => receipts.Sum(r => r.TotalAmount);

    public decimal GetAverageReceipt(List<Receipt> receipts)
    {
        if (receipts.Count == 0) return 0;   // avoid dividing by zero
        return GetTotalSpent(receipts) / receipts.Count;
    }

    /// <summary>Money spent in each of the last N months (oldest first). Months without receipts show 0.</summary>
    public List<ChartBar> GetSpendingByMonth(List<Receipt> receipts, int months)
    {
        var rows = new List<(string Label, double Value)>();
        DateTime firstOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        for (int monthsAgo = months - 1; monthsAgo >= 0; monthsAgo--)
        {
            DateTime month = firstOfThisMonth.AddMonths(-monthsAgo);

            decimal total = receipts
                .Where(r => r.PurchaseDate.Year == month.Year && r.PurchaseDate.Month == month.Month)
                .Sum(r => r.TotalAmount);

            rows.Add((month.ToString("MMM yyyy"), (double)total));
        }

        return MakeBars(rows, FormatMoney);
    }

    /// <summary>Money spent per store, biggest first.</summary>
    public List<ChartBar> GetSpendingByStore(List<Receipt> receipts)
    {
        var rows = receipts
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Store) ? "Unknown store" : r.Store.Trim().ToUpper())
            .Select(group => (Label: group.Key, Value: (double)group.Sum(r => r.TotalAmount)))
            .OrderByDescending(row => row.Value)
            .ToList();

        return MakeBars(rows, FormatMoney);
    }

    /// <summary>The products we spent the most money on (top 5).</summary>
    public List<ChartBar> GetTopProducts(List<Receipt> receipts, int howMany = 5)
    {
        var rows = receipts
            .SelectMany(r => r.Items)                       // all receipt lines of all receipts in one list
            .GroupBy(item => item.ProductName)              // one group per product name
            .Select(group => (Label: group.Key, Value: (double)group.Sum(item => item.LineTotal)))
            .OrderByDescending(row => row.Value)
            .Take(howMany)
            .ToList();

        return MakeBars(rows, FormatMoney);
    }

    /// <summary>How many pantry items are fresh, expiring soon, expired or have no date.</summary>
    public List<ChartBar> GetPantryFreshness(List<PantryItem> pantryItems)
    {
        // PantryItem already knows its own ExpiryStatus, so we only count.
        int fresh = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.Normal);
        int soon = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.ExpiringSoon);
        int expired = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.Expired);
        int noDate = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.NoDate);

        var rows = new List<(string Label, double Value)>
        {
            ("Fresh", fresh),
            ("Use soon", soon),
            ("Expired", expired),
            ("No date", noDate)
        };

        return MakeBars(rows, FormatCount);
    }

    /// <summary>How many recipes each category has (top 8).</summary>
    public List<ChartBar> GetRecipesByCategory(List<Recipe> recipes, int howMany = 8)
    {
        var rows = recipes
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Category) ? "Other" : r.Category)
            .Select(group => (Label: group.Key, Value: (double)group.Count()))
            .OrderByDescending(row => row.Value)
            .Take(howMany)
            .ToList();

        return MakeBars(rows, FormatCount);
    }

    public List<string> GetInsights(List<Receipt> receipts, List<PantryItem> pantryItems)
    {
        var insights = new List<string>();

        if (receipts.Count > 0)
        {
            var topStore = GetSpendingByStore(receipts).First();
            insights.Add($"You spent the most at {topStore.Label} ({topStore.ValueText}).");

            var biggest = receipts.OrderByDescending(r => r.TotalAmount).First();
            insights.Add($"Your biggest shop was {FormatMoney((double)biggest.TotalAmount)} at {biggest.Store} on {biggest.PurchaseDate:yyyy-MM-dd}.");
        }
        else
        {
            insights.Add("No receipts in this period. Add one on the Receipts page to see spending insights.");
        }

        int expired = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.Expired);
        int soon = pantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.ExpiringSoon);

        if (expired > 0) insights.Add($"{expired} pantry item(s) have expired. Check the Pantry page.");
        if (soon > 0) insights.Add($"{soon} pantry item(s) expire within 3 days. Cook them soon!");
        if (expired == 0 && soon == 0 && pantryItems.Count > 0) insights.Add("Nothing in your pantry is about to expire. Nice!");

        return insights;
    }

    // Eating habits: statistics about the food the user really ate (from the meal log)

    /// <summary>Keeps only meals from the last N months (this month counts as month 1), same rule as FilterByMonths.</summary>
    public List<MealLogEntry> FilterMealsByMonths(List<MealLogEntry> meals, int months)
    {
        DateTime firstOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime startDate = firstOfThisMonth.AddMonths(-(months - 1));

        return meals.Where(m => m.EatenAt >= startDate).ToList();
    }

    /// <summary>The four numbers at the top: meals, average per day, average per meal ...</summary>
    public ConsumptionSummary GetConsumptionSummary(List<MealLogEntry> meals)
    {
        int days = meals.Select(m => m.EatenAt.Date).Distinct().Count();
        double total = meals.Sum(m => m.Calories);

        return new ConsumptionSummary
        {
            MealsLogged = meals.Count,
            DaysLogged = days,
            TotalCalories = total,
            AverageDailyCalories = days > 0 ? total / days : 0,
            AverageCaloriesPerMeal = meals.Count > 0 ? total / meals.Count : 0
        };
    }

    /// <summary>
    /// How many days in a row (ending today, or yesterday if nothing is logged yet today) have at least one logged meal.
    /// </summary>
    public int GetLoggingStreak(List<MealLogEntry> meals)
    {
        var days = meals.Select(m => m.EatenAt.Date).ToHashSet();

        DateTime day = DateTime.Today;
        if (!days.Contains(day)) day = day.AddDays(-1);   // today may simply not be logged yet

        int streak = 0;
        while (days.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }

    /// <summary>Calories eaten on each of the last N days (oldest first). Days without logged meals show 0.</summary>
    public List<ChartBar> GetCaloriesPerDay(List<MealLogEntry> meals, int days = 7)
    {
        var rows = new List<(string Label, double Value)>();

        for (int daysAgo = days - 1; daysAgo >= 0; daysAgo--)
        {
            DateTime day = DateTime.Today.AddDays(-daysAgo);
            double kcal = meals.Where(m => m.EatenAt.Date == day).Sum(m => m.Calories);

            rows.Add((day.ToString("ddd d MMM", CultureInfo.InvariantCulture), kcal));
        }

        return MakeBars(rows, FormatKcal);
    }

    /// <summary>How many breakfasts, lunches, dinners and snacks were logged.</summary>
    public List<ChartBar> GetMealsByType(List<MealLogEntry> meals)
    {
        var rows = new List<(string Label, double Value)>();

        foreach (MealType type in Enum.GetValues<MealType>())
            rows.Add((type.ToString(), meals.Count(m => m.MealType == type)));

        return MakeBars(rows, FormatCount);
    }

    /// <summary>The recipes eaten most often (top 5).</summary>
    public List<ChartBar> GetMostEatenRecipes(List<MealLogEntry> meals, int howMany = 5)
    {
        var rows = meals
            .GroupBy(m => m.RecipeName)
            .Select(group => (Label: group.Key, Value: (double)group.Count()))
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Label)
            .Take(howMany)
            .ToList();

        return MakeBars(rows, count => $"{count:0}x");
    }

    /// <summary>Which cuisines the user eats most (top 6).</summary>
    public List<ChartBar> GetCuisineBreakdown(List<MealLogEntry> meals, int howMany = 6)
    {
        var rows = meals
            .GroupBy(m => string.IsNullOrWhiteSpace(m.Cuisine) ? "Other" : m.Cuisine)
            .Select(group => (Label: group.Key, Value: (double)group.Count()))
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Label)
            .Take(howMany)
            .ToList();

        return MakeBars(rows, FormatCount);
    }

    /// <summary>Average calories per weekday (Monday to Sunday), counting only the days that were logged.</summary>
    public List<ChartBar> GetCaloriesByWeekday(List<MealLogEntry> meals)
    {
        var rows = new List<(string Label, double Value)>();

        foreach (DayOfWeek weekday in WeekdaysMondayFirst)
        {
            var mealsOnThisWeekday = meals.Where(m => m.EatenAt.DayOfWeek == weekday).ToList();
            int daysLogged = mealsOnThisWeekday.Select(m => m.EatenAt.Date).Distinct().Count();
            double average = daysLogged > 0 ? mealsOnThisWeekday.Sum(m => m.Calories) / daysLogged : 0;

            rows.Add((weekday.ToString(), average));
        }

        return MakeBars(rows, FormatKcal);
    }

    /// <summary>
    /// Share of the energy that comes from protein, carbohydrates and fat (4, 4 and 9 kcal per gram).
    /// Only meals that have macro values are used, because unknown values must not be counted as 0.
    /// </summary>
    public List<ChartBar> GetMacroSplit(List<MealLogEntry> meals)
    {
        var withMacros = meals.Where(m => m.HasMacros).ToList();

        double protein = withMacros.Sum(m => m.Protein ?? 0) * 4;
        double carbs = withMacros.Sum(m => m.Carbs ?? 0) * 4;
        double fat = withMacros.Sum(m => m.Fat ?? 0) * 9;
        double total = protein + carbs + fat;

        var rows = new List<(string Label, double Share)>
        {
            ("Protein", total > 0 ? protein / total * 100 : 0),
            ("Carbs", total > 0 ? carbs / total * 100 : 0),
            ("Fat", total > 0 ? fat / total * 100 : 0)
        };

        // Here the bar IS the percentage (a share chart), so we do not use MakeBars.
        return rows.Select(row => new ChartBar
        {
            Label = row.Label,
            Value = row.Share,
            ValueText = $"{row.Share:0}%",
            Percent = row.Share
        }).ToList();
    }

    /// <summary>Plain-English sentences about the eating habits.</summary>
    public List<string> GetConsumptionInsights(List<MealLogEntry> meals, int targetCalories = 2000)
    {
        var insights = new List<string>();

        if (meals.Count == 0)
        {
            insights.Add("No meals logged in this period. Open the Menu page and press \"I ate this\" to start tracking.");
            return insights;
        }

        var summary = GetConsumptionSummary(meals);

        // 1. Calories against the daily target
        double percentOfTarget = summary.AverageDailyCalories / targetCalories * 100;
        insights.Add($"On the days you logged, you ate {summary.AverageDailyCalories:N0} kcal on average ({percentOfTarget:0}% of a {targetCalories:N0} kcal day). Food you did not log is not included.");

        // 2. Favourites
        var topRecipe = GetMostEatenRecipes(meals, 1).First();
        insights.Add($"Your most eaten recipe is {topRecipe.Label} ({topRecipe.ValueText}).");

        var topCuisine = GetCuisineBreakdown(meals, 1).First();
        insights.Add($"Your favourite cuisine is {topCuisine.Label} ({topCuisine.ValueText.TrimEnd()} meals).");

        // 3. The heaviest weekday
        var busiestDay = GetCaloriesByWeekday(meals).OrderByDescending(b => b.Value).First();
        if (busiestDay.Value > 0)
            insights.Add($"{busiestDay.Label} is your heaviest day: about {busiestDay.Value:N0} kcal.");

        // 4. Snacks and desserts
        double sweetKcal = meals
            .Where(m => m.Category.Equals("Snack", StringComparison.OrdinalIgnoreCase) ||
                        m.Category.Equals("Dessert", StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.Calories);
        if (summary.TotalCalories > 0)
            insights.Add($"{sweetKcal / summary.TotalCalories * 100:0}% of your logged calories come from snacks and desserts.");

        // 5. The biggest single meal
        var biggest = meals.OrderByDescending(m => m.Calories).First();
        insights.Add($"Your biggest meal was {biggest.RecipeName} ({biggest.Calories:N0} kcal) on {biggest.EatenAt:yyyy-MM-dd}.");

        // 6. Streak
        int streak = GetLoggingStreak(meals);
        if (streak >= 2) insights.Add($"You have logged your meals {streak} days in a row.");

        // 7. Honesty about missing data
        int withMacros = meals.Count(m => m.HasMacros);
        if (withMacros < meals.Count)
            insights.Add($"Protein, carbs and fat are known for only {withMacros} of {meals.Count} logged meals, so the macro split is partial.");

        return insights;
    }

    /// <summary>
    /// Turns (label, value) rows into ChartBars and works out each bar length.
    /// Example: values 50, 100, 25  ->  percents 50, 100, 25   (the biggest value is always 100).
    /// </summary>
    private static List<ChartBar> MakeBars(List<(string Label, double Value)> rows, Func<double, string> formatValue)
    {
        double biggest = rows.Count > 0 ? rows.Max(r => r.Value) : 0;
        var bars = new List<ChartBar>();

        foreach (var row in rows)
        {
            bars.Add(new ChartBar
            {
                Label = row.Label,
                Value = row.Value,
                ValueText = formatValue(row.Value),
                Percent = biggest > 0 ? row.Value / biggest * 100 : 0
            });
        }

        return bars;
    }

    private static string FormatMoney(double amount) => $"{amount:N0} Ft";

    private static string FormatCount(double count) => $"{count:0}";

    private static string FormatKcal(double kcal) => $"{kcal:N0} kcal";

    private static readonly DayOfWeek[] WeekdaysMondayFirst =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };
}
