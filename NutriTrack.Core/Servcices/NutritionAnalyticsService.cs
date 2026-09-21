using System.IO;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class NutritionAnalyticsService
{
    private readonly List<ProductConsumption> _consumptionLogs = new();

    public void LogConsumption(Product product, double gramsConsumed, DateTime date)
    {
        _consumptionLogs.Add(new ProductConsumption
        {
            Product = product,
            AmountConsumed = gramsConsumed,
            ConsumedAt = date
        });
    }

    /// <summary>Calculates total nutrition consumed within a specific date range.</summary>
    public NutritionSummary GetSummaryForDateRange(DateTime startDate, DateTime endDate, int? productId = null)
    {
        var query = _consumptionLogs
            .Where(c => c.ConsumedAt >= startDate && c.ConsumedAt <= endDate);

        // Filter by a specific product if provided
        if (productId.HasValue)
        {
            query = query.Where(c => c.Product.ProductId == productId.Value);
        }

        var logs = query.ToList();

        return new NutritionSummary
        {
            TotalCalories = logs.Sum(c => c.TotalCalories),
            TotalProtein = logs.Sum(c => c.TotalProtein),
            TotalCarbs = logs.Sum(c => c.TotalCarbs),
            TotalFat = logs.Sum(c => c.TotalFat),
            TotalFiber = logs.Sum(c => c.TotalFiber),
            TotalSugar = logs.Sum(c => c.TotalSugar),
            TotalSalt = logs.Sum(c => c.TotalSalt),
            TotalGramsConsumed = logs.Sum(c => c.AmountConsumed)
        };
    }

    // Preset helper methods for Daily, Weekly, and Yearly queries
    public NutritionSummary GetDailySummary(DateTime date, int? productId = null)
        => GetSummaryForDateRange(date.Date, date.Date.AddDays(1).AddTicks(-1), productId);

    public NutritionSummary GetWeeklySummary(DateTime startOfWeek, int? productId = null)
        => GetSummaryForDateRange(startOfWeek.Date, startOfWeek.Date.AddDays(7).AddTicks(-1), productId);

    public NutritionSummary GetYearlySummary(int year, int? productId = null)
        => GetSummaryForDateRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59), productId);
}