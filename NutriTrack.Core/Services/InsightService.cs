using System.Globalization;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Turns real data into short sentences. Each rule below has a minimum amount of data;
/// if the data is too thin, the rule adds nothing (no weak or made-up insights).
/// </summary>
public static class InsightService
{
    public static List<string> Generate(
        DateRange range,
        IReadOnlyCollection<Receipt> receipts,
        IReadOnlyCollection<PantryItem> pantry,
        IReadOnlyCollection<CookingEvent> cooking,
        IReadOnlyCollection<WasteEvent> waste,
        string currency)
    {
        var insights = new List<string>();

        AddSpendingChange(insights, range, receipts);
        AddExpiryWarnings(insights, pantry);
        AddAverageReceipt(insights, range, receipts, currency);
        AddFavouriteCuisine(insights, range, cooking);
        AddWasteConcentration(insights, range, waste);

        return insights;
    }

    // Rule 1: spending went up/down by at least 10% (needs receipts in both periods).
    private static void AddSpendingChange(List<string> insights, DateRange range, IReadOnlyCollection<Receipt> receipts)
    {
        PeriodComparison change = StatisticsService.CompareSpending(receipts, range);
        if (change.Percent == null) return;

        double percent = change.Percent.Value;
        if (Math.Abs(percent) < 10) return;

        string direction = percent > 0 ? "increased" : "decreased";
        insights.Add($"Your spending {direction} {Math.Abs(percent):0}% compared with the previous period.");
    }

    // Rule 2: items that expire soon, and items that already expired.
    private static void AddExpiryWarnings(List<string> insights, IReadOnlyCollection<PantryItem> pantry)
    {
        int expiringSoon = pantry.Count(i => i.ExpiryStatus == ExpiryStatus.ExpiringSoon);
        if (expiringSoon > 0)
        {
            string noun = expiringSoon == 1 ? "item" : "items";
            insights.Add($"You have {expiringSoon} {noun} expiring within 3 days.");
        }

        int expired = pantry.Count(i => i.ExpiryStatus == ExpiryStatus.Expired);
        if (expired > 0)
        {
            string verb = expired == 1 ? "item has" : "items have";
            insights.Add($"{expired} pantry {verb} already expired.");
        }
    }

    // Rule 3: average receipt, only with at least 3 receipts.
    private static void AddAverageReceipt(List<string> insights, DateRange range, IReadOnlyCollection<Receipt> receipts, string currency)
    {
        SpendingStats spending = StatisticsService.Spending(receipts, range);
        if (spending.ReceiptCount < 3) return;

        string amount = Math.Round(spending.AverageReceipt).ToString("N0", CultureInfo.CurrentCulture);
        insights.Add($"Your average receipt is {amount} {currency}".TrimEnd() + ".");
    }

    // Rule 4: a cuisine cooked at least twice, out of at least 3 cooked recipes.
    private static void AddFavouriteCuisine(List<string> insights, DateRange range, IReadOnlyCollection<CookingEvent> cooking)
    {
        CookingStats stats = StatisticsService.Cooking(cooking, range, new List<Recipe>());
        if (stats.TimesCooked < 3 || stats.Cuisines.Count == 0) return;

        NamedCount top = stats.Cuisines[0];
        if (top.Count < 2) return;

        insights.Add($"{top.Name} recipes are your most frequently cooked cuisine.");
    }

    // Rule 5: one category makes up at least half of at least 3 waste events.
    private static void AddWasteConcentration(List<string> insights, DateRange range, IReadOnlyCollection<WasteEvent> waste)
    {
        WasteStats stats = StatisticsService.Waste(waste, range);
        if (stats.Events < 3 || stats.ByCategory.Count == 0) return;

        NamedCount top = stats.ByCategory[0];
        bool isHalfOrMore = top.Count * 2 >= stats.Events;
        if (!isHalfOrMore || top.Name == "Other") return;

        insights.Add($"{top.Name} represents most of your recorded waste.");
    }
}
