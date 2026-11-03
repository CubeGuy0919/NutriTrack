using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// All the numbers for the Analytics screen.
///
/// How to read this file:
///   - It does NOT load files and does NOT know about WPF. It only receives lists
///     (receipts, pantry items, recipes) and gives back simple results.
///   - Every method does one small job, so you can understand (and test) them one by one.
///   - Most methods use LINQ: Where = filter, GroupBy = make groups, Sum = add up,
///     OrderByDescending = biggest first, Take = keep only the first few.
/// </summary>
public class AnalyticsService
{
    // ---------------------------------------------------------------
    // Step 1: pick the receipts we want to analyse
    // ---------------------------------------------------------------

    /// <summary>Keeps only receipts from the last N months (this month counts as month 1).</summary>
    public List<Receipt> FilterByMonths(List<Receipt> receipts, int months)
    {
        DateTime firstOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime startDate = firstOfThisMonth.AddMonths(-(months - 1));

        return receipts.Where(r => r.PurchaseDate >= startDate).ToList();
    }

    // ---------------------------------------------------------------
    // Step 2: the three big numbers at the top
    // ---------------------------------------------------------------

    public decimal GetTotalSpent(List<Receipt> receipts) => receipts.Sum(r => r.TotalAmount);

    public decimal GetAverageReceipt(List<Receipt> receipts)
    {
        if (receipts.Count == 0) return 0;   // avoid dividing by zero
        return GetTotalSpent(receipts) / receipts.Count;
    }

    // ---------------------------------------------------------------
    // Step 3: the charts (each one returns a list of ChartBar)
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // Step 4: short sentences ("insights") in plain English
    // ---------------------------------------------------------------

    public List<string> GetInsights(List<Receipt> receipts, List<PantryItem> pantryItems)
    {
        var insights = new List<string>();

        if (receipts.Count > 0)
        {
            // Store where we spent the most
            var topStore = GetSpendingByStore(receipts).First();
            insights.Add($"You spent the most at {topStore.Label} ({topStore.ValueText}).");

            // Most expensive shopping trip
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

    // ---------------------------------------------------------------
    // Small helpers
    // ---------------------------------------------------------------

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
}
