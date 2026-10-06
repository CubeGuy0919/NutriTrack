using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

// ============================================================================
// Statistics: pure calculations. You give it data, it gives you numbers.
// It never reads files and never invents values. "No data" is reported as
// empty lists, 0 counts, or null - never as fake numbers.
// ============================================================================

public enum RangePreset { Today, Yesterday, Last7Days, Last30Days, Last90Days, ThisYear, Custom }

public enum NutritionMetric { Calories, Protein, Carbs, Fat, Fiber, Sugar, Salt }

/// <summary>A span of whole days. Start is included, EndExclusive is the first day NOT included.</summary>
public class DateRange
{
    public DateTime Start { get; }
    public DateTime EndExclusive { get; }

    public DateRange(DateTime start, DateTime endExclusive)
    {
        Start = start;
        EndExclusive = endExclusive;
    }

    public int Days
    {
        get
        {
            int days = (int)(EndExclusive - Start).TotalDays;
            return days < 1 ? 1 : days;
        }
    }

    public bool Contains(DateTime time) => time >= Start && time < EndExclusive;

    /// <summary>The range of the same length that ends right where this one starts.</summary>
    public DateRange Previous() => new DateRange(Start.AddDays(-Days), Start);

    /// <summary>Builds the range for a preset such as "Last 7 days". Custom uses the two given dates (both included).</summary>
    public static DateRange From(RangePreset preset, DateTime today, DateTime? customStart = null, DateTime? customEnd = null)
    {
        today = today.Date;
        DateTime tomorrow = today.AddDays(1);

        if (preset == RangePreset.Today) return new DateRange(today, tomorrow);
        if (preset == RangePreset.Yesterday) return new DateRange(today.AddDays(-1), today);
        if (preset == RangePreset.Last7Days) return new DateRange(today.AddDays(-6), tomorrow);
        if (preset == RangePreset.Last30Days) return new DateRange(today.AddDays(-29), tomorrow);
        if (preset == RangePreset.Last90Days) return new DateRange(today.AddDays(-89), tomorrow);
        if (preset == RangePreset.ThisYear) return new DateRange(new DateTime(today.Year, 1, 1), tomorrow);

        // Custom: if the user picked the dates the wrong way round, swap them.
        DateTime start = (customStart ?? today).Date;
        DateTime end = (customEnd ?? today).Date;
        if (end < start)
        {
            DateTime swap = start;
            start = end;
            end = swap;
        }
        return new DateRange(start, end.AddDays(1));
    }
}

// ---- Small result types ----

/// <summary>"Spending +12%" or "Not enough data". Percent is null when there is no honest comparison.</summary>
public class PeriodComparison
{
    public double? Percent { get; }
    public string Text { get; }

    public PeriodComparison(double? percent, string text)
    {
        Percent = percent;
        Text = text;
    }

    public static PeriodComparison NotEnoughData => new PeriodComparison(null, "Not enough data");
}

/// <summary>One point of a chart: a day and a value.</summary>
public class DailyValue
{
    public DateTime Date { get; }
    public double Value { get; }
    public DailyValue(DateTime date, double value) { Date = date; Value = value; }
}

/// <summary>A money amount with its share (in percent) of the total, e.g. "Lidl: 12 000 Ft = 40%".</summary>
public class NamedAmount
{
    public string Name { get; }
    public decimal Amount { get; }
    public double SharePercent { get; }
    public NamedAmount(string name, decimal amount, double sharePercent) { Name = name; Amount = amount; SharePercent = sharePercent; }
}

/// <summary>A label with a count, e.g. "Italian: 4 times".</summary>
public class NamedCount
{
    public string Name { get; }
    public int Count { get; }
    public NamedCount(string name, int count) { Name = name; Count = count; }
}

public class ConsumedProduct
{
    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public int Times { get; set; }
    public double Calories { get; set; }
}

public class NutritionStats
{
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }
    public double Salt { get; set; }

    public int MealsLogged { get; set; }
    public int UniqueFoods { get; set; }
    public int DaysWithData { get; set; }
    public double TotalQuantity { get; set; }

    public bool HasData => MealsLogged > 0;

    /// <summary>Protein / carbs / fat as percent of their combined grams. All 0 when nothing was logged.</summary>
    public double ProteinPercent => MacroPercent(Protein);
    public double CarbsPercent => MacroPercent(Carbs);
    public double FatPercent => MacroPercent(Fat);

    private double MacroPercent(double grams)
    {
        double total = Protein + Carbs + Fat;
        return total <= 0 ? 0 : grams / total * 100;
    }
}

public class SpendingStats
{
    public decimal Total { get; set; }
    public int ReceiptCount { get; set; }
    public decimal AveragePerDay { get; set; }
    public bool HasData => ReceiptCount > 0;
    public decimal AverageReceipt => ReceiptCount == 0 ? 0 : Total / ReceiptCount;
}

public class CookingStats
{
    public int TimesCooked { get; set; }
    public double? AverageMinutes { get; set; }
    public double? AverageCalories { get; set; }
    public List<NamedCount> MostCooked { get; set; } = new();
    public List<NamedCount> Cuisines { get; set; } = new();
    public List<NamedCount> Categories { get; set; } = new();
    public List<NamedCount> Difficulty { get; set; } = new();
    public List<Recipe> NeverCooked { get; set; } = new();
}

public class WasteStats
{
    public int Events { get; set; }
    public decimal KnownValue { get; set; }
    public int EventsWithUnknownValue { get; set; }
    public List<NamedCount> ByCategory { get; set; } = new();
    public bool HasData => Events > 0;
}

public static class StatisticsService
{
    // ========================================================================
    // NUTRITION
    // ========================================================================

    /// <summary>Adds up everything eaten inside the range.</summary>
    public static NutritionStats Nutrition(IEnumerable<ConsumptionEvent> events, DateRange range)
    {
        List<ConsumptionEvent> inRange = EventsInRange(events, range);

        var stats = new NutritionStats();
        var foodNames = new HashSet<string>();
        var days = new HashSet<DateTime>();

        foreach (ConsumptionEvent e in inRange)
        {
            stats.Calories += e.Calories;
            stats.Protein += e.Protein;
            stats.Carbs += e.Carbs;
            stats.Fat += e.Fat;
            stats.Fiber += e.Fiber;
            stats.Sugar += e.Sugar;
            stats.Salt += e.Salt;
            stats.MealsLogged++;

            // Only weights/volumes can be added together; "servings" cannot.
            if (e.Unit == "g" || e.Unit == "ml") stats.TotalQuantity += e.Quantity;

            foodNames.Add(e.ProductName.Trim().ToLowerInvariant());
            days.Add(e.Timestamp.Date);
        }

        stats.UniqueFoods = foodNames.Count;
        stats.DaysWithData = days.Count;
        return stats;
    }

    /// <summary>The value of one nutrient for one event (used to pick the metric a chart shows).</summary>
    public static double MetricValue(ConsumptionEvent e, NutritionMetric metric)
    {
        if (metric == NutritionMetric.Calories) return e.Calories;
        if (metric == NutritionMetric.Protein) return e.Protein;
        if (metric == NutritionMetric.Carbs) return e.Carbs;
        if (metric == NutritionMetric.Fat) return e.Fat;
        if (metric == NutritionMetric.Fiber) return e.Fiber;
        if (metric == NutritionMetric.Sugar) return e.Sugar;
        return e.Salt;
    }

    /// <summary>
    /// One value per day for a line chart. Days where nothing was logged are NOT in the list:
    /// "nothing logged" is unknown, not zero, so a chart should not draw it as 0.
    /// </summary>
    public static List<DailyValue> DailySeries(IEnumerable<ConsumptionEvent> events, DateRange range, NutritionMetric metric)
    {
        var totalPerDay = new SortedDictionary<DateTime, double>();

        foreach (ConsumptionEvent e in EventsInRange(events, range))
        {
            DateTime day = e.Timestamp.Date;
            totalPerDay.TryGetValue(day, out double sofar);
            totalPerDay[day] = sofar + MetricValue(e, metric);
        }

        var result = new List<DailyValue>();
        foreach (var pair in totalPerDay) result.Add(new DailyValue(pair.Key, pair.Value));
        return result;
    }

    /// <summary>Most eaten foods, ranked by how often they were logged (ties: more calories first).</summary>
    public static List<ConsumedProduct> TopConsumed(IEnumerable<ConsumptionEvent> events, DateRange range, int take = 10)
    {
        var byName = new Dictionary<string, ConsumedProduct>(StringComparer.OrdinalIgnoreCase);

        foreach (ConsumptionEvent e in EventsInRange(events, range))
        {
            string name = e.ProductName.Trim();
            if (!byName.TryGetValue(name, out ConsumedProduct? item))
            {
                item = new ConsumedProduct { Name = name, Unit = e.Unit };
                byName[name] = item;
            }
            item.Quantity += e.Quantity;
            item.Calories += e.Calories;
            item.Times++;
        }

        return byName.Values
            .OrderByDescending(p => p.Times)
            .ThenByDescending(p => p.Calories)
            .Take(take)
            .ToList();
    }

    /// <summary>Most eaten food categories (events without a category are skipped, not guessed).</summary>
    public static List<NamedCount> TopConsumedCategories(IEnumerable<ConsumptionEvent> events, DateRange range, int take = 8)
    {
        var names = new List<string>();
        foreach (ConsumptionEvent e in EventsInRange(events, range))
        {
            if (!string.IsNullOrWhiteSpace(e.Category)) names.Add(e.Category);
        }
        return CountNames(names).Take(take).ToList();
    }

    // ========================================================================
    // COMPARISON WITH THE PREVIOUS PERIOD
    // ========================================================================

    /// <summary>
    /// Percent change from previous to current. If either period has no data, or the previous value is 0
    /// (division by zero / meaningless), the answer is "Not enough data".
    /// </summary>
    public static PeriodComparison Compare(double current, double previous, bool hasCurrentData, bool hasPreviousData)
    {
        if (!hasCurrentData || !hasPreviousData || previous <= 0)
            return PeriodComparison.NotEnoughData;

        double percent = (current - previous) / previous * 100.0;

        if (Math.Abs(percent) < 0.5)
            return new PeriodComparison(percent, "No change vs previous period");

        string arrow = percent > 0 ? "↑" : "↓";
        return new PeriodComparison(percent, $"{arrow} {Math.Abs(percent):0}% vs previous period");
    }

    public static PeriodComparison CompareSpending(IEnumerable<Receipt> receipts, DateRange range)
    {
        SpendingStats now = Spending(receipts, range);
        SpendingStats before = Spending(receipts, range.Previous());
        return Compare((double)now.Total, (double)before.Total, now.HasData, before.HasData);
    }

    public static PeriodComparison CompareNutrition(IEnumerable<ConsumptionEvent> events, DateRange range, NutritionMetric metric)
    {
        List<ConsumptionEvent> all = events.ToList();
        List<ConsumptionEvent> now = EventsInRange(all, range);
        List<ConsumptionEvent> before = EventsInRange(all, range.Previous());

        double nowTotal = now.Sum(e => MetricValue(e, metric));
        double beforeTotal = before.Sum(e => MetricValue(e, metric));
        return Compare(nowTotal, beforeTotal, now.Count > 0, before.Count > 0);
    }

    // ========================================================================
    // SPENDING
    // ========================================================================

    public static SpendingStats Spending(IEnumerable<Receipt> receipts, DateRange range)
    {
        List<Receipt> inRange = ReceiptsInRange(receipts, range);
        decimal total = inRange.Sum(r => r.TotalAmount);

        return new SpendingStats
        {
            Total = total,
            ReceiptCount = inRange.Count,
            AveragePerDay = total / range.Days
        };
    }

    /// <summary>Money spent per day (only days with a receipt).</summary>
    public static List<DailyValue> SpendingSeries(IEnumerable<Receipt> receipts, DateRange range)
    {
        var totalPerDay = new SortedDictionary<DateTime, decimal>();

        foreach (Receipt r in ReceiptsInRange(receipts, range))
        {
            DateTime day = r.PurchaseDate.Date;
            totalPerDay.TryGetValue(day, out decimal sofar);
            totalPerDay[day] = sofar + r.TotalAmount;
        }

        var result = new List<DailyValue>();
        foreach (var pair in totalPerDay) result.Add(new DailyValue(pair.Key, (double)pair.Value));
        return result;
    }

    public static List<NamedAmount> SpendingByStore(IEnumerable<Receipt> receipts, DateRange range)
    {
        var amounts = new List<KeyValuePair<string, decimal>>();
        foreach (Receipt r in ReceiptsInRange(receipts, range))
        {
            string store = string.IsNullOrWhiteSpace(r.Store) ? "Unknown store" : r.Store.Trim();
            amounts.Add(new KeyValuePair<string, decimal>(store, r.TotalAmount));
        }
        return SumByNameWithShare(amounts);
    }

    public static List<NamedAmount> SpendingByProduct(IEnumerable<Receipt> receipts, DateRange range, int take = 10)
    {
        var amounts = new List<KeyValuePair<string, decimal>>();
        foreach (Receipt r in ReceiptsInRange(receipts, range))
        {
            foreach (ReceiptItem item in r.Items)
            {
                string name = string.IsNullOrWhiteSpace(item.ProductName) ? "Unnamed item" : item.ProductName.Trim();
                amounts.Add(new KeyValuePair<string, decimal>(name, item.LineTotal));
            }
        }
        return SumByNameWithShare(amounts).Take(take).ToList();
    }

    /// <summary>categoryOf(productId) returns the product's category or null. Unknown categories become "Other".</summary>
    public static List<NamedAmount> SpendingByCategory(IEnumerable<Receipt> receipts, DateRange range, Func<int, string?> categoryOf)
    {
        var amounts = new List<KeyValuePair<string, decimal>>();
        foreach (Receipt r in ReceiptsInRange(receipts, range))
        {
            foreach (ReceiptItem item in r.Items)
            {
                string? category = categoryOf(item.ProductId);
                if (string.IsNullOrWhiteSpace(category)) category = "Other";
                amounts.Add(new KeyValuePair<string, decimal>(category, item.LineTotal));
            }
        }
        return SumByNameWithShare(amounts);
    }

    /// <summary>
    /// Adds amounts with the same name together, sorts biggest first, and computes each share of the grand total
    /// (so the shares always add up to 100%).
    /// </summary>
    private static List<NamedAmount> SumByNameWithShare(List<KeyValuePair<string, decimal>> amounts)
    {
        var sums = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in amounts)
        {
            sums.TryGetValue(pair.Key, out decimal sofar);
            sums[pair.Key] = sofar + pair.Value;
        }

        decimal grandTotal = sums.Values.Where(v => v > 0).Sum();

        var result = new List<NamedAmount>();
        foreach (var pair in sums.OrderByDescending(p => p.Value))
        {
            if (pair.Value <= 0) continue;
            double share = (double)(pair.Value / grandTotal * 100m);
            result.Add(new NamedAmount(pair.Key, pair.Value, share));
        }
        return result;
    }

    // ========================================================================
    // COOKING
    // ========================================================================

    public static CookingStats Cooking(IEnumerable<CookingEvent> events, DateRange range, IEnumerable<Recipe> allRecipes)
    {
        List<CookingEvent> all = events.ToList();
        List<CookingEvent> inRange = all.Where(e => range.Contains(e.Timestamp)).ToList();

        // "Never cooked" looks at ALL history, not just the selected range.
        var cookedIds = new HashSet<int>(all.Select(e => e.RecipeId));

        // Averages only use events that actually have the value.
        List<CookingEvent> withTime = inRange.Where(e => e.TotalTimeMinutes > 0).ToList();
        List<CookingEvent> withCalories = inRange.Where(e => e.Calories > 0 && e.Servings > 0).ToList();

        return new CookingStats
        {
            TimesCooked = inRange.Count,
            AverageMinutes = withTime.Count == 0 ? null : withTime.Average(e => (double)e.TotalTimeMinutes),
            AverageCalories = withCalories.Count == 0 ? null : withCalories.Average(e => e.Calories / e.Servings),
            MostCooked = CountNames(inRange.Select(e => e.RecipeName)).Take(10).ToList(),
            Cuisines = CountNames(inRange.Select(e => e.Cuisine)),
            Categories = CountNames(inRange.Select(e => e.Category)),
            Difficulty = CountNames(inRange.Select(e => e.Difficulty)),
            NeverCooked = allRecipes.Where(r => !cookedIds.Contains(r.RecipeId)).OrderBy(r => r.Name).ToList()
        };
    }

    // ========================================================================
    // WASTE
    // ========================================================================

    public static WasteStats Waste(IEnumerable<WasteEvent> events, DateRange range)
    {
        List<WasteEvent> inRange = events.Where(w => range.Contains(w.Timestamp)).ToList();

        var categories = new List<string>();
        foreach (WasteEvent w in inRange)
            categories.Add(string.IsNullOrWhiteSpace(w.Category) ? "Other" : w.Category);

        return new WasteStats
        {
            Events = inRange.Count,
            // Only add values that are really known; count the rest so the screen can say "n items without price".
            KnownValue = inRange.Where(w => w.EstimatedValue.HasValue).Sum(w => w.EstimatedValue!.Value),
            EventsWithUnknownValue = inRange.Count(w => !w.EstimatedValue.HasValue),
            ByCategory = CountNames(categories)
        };
    }

    /// <summary>Number of wasted items per day.</summary>
    public static List<DailyValue> WasteSeries(IEnumerable<WasteEvent> events, DateRange range)
    {
        var countPerDay = new SortedDictionary<DateTime, int>();

        foreach (WasteEvent w in events.Where(w => range.Contains(w.Timestamp)))
        {
            DateTime day = w.Timestamp.Date;
            countPerDay.TryGetValue(day, out int sofar);
            countPerDay[day] = sofar + 1;
        }

        var result = new List<DailyValue>();
        foreach (var pair in countPerDay) result.Add(new DailyValue(pair.Key, pair.Value));
        return result;
    }

    // ========================================================================
    // SMALL SHARED HELPERS
    // ========================================================================

    private static List<ConsumptionEvent> EventsInRange(IEnumerable<ConsumptionEvent> events, DateRange range) =>
        events.Where(e => range.Contains(e.Timestamp)).ToList();

    private static List<Receipt> ReceiptsInRange(IEnumerable<Receipt> receipts, DateRange range) =>
        receipts.Where(r => range.Contains(r.PurchaseDate)).ToList();

    /// <summary>Counts how often each name appears (ignoring upper/lower case), most frequent first. Blank names are skipped.</summary>
    private static List<NamedCount> CountNames(IEnumerable<string> names)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            string clean = name.Trim();
            counts.TryGetValue(clean, out int sofar);
            counts[clean] = sofar + 1;
        }

        return counts
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key)
            .Select(p => new NamedCount(p.Key, p.Value))
            .ToList();
    }
}
