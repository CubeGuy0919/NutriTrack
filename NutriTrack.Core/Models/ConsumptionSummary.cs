namespace NutriTrack.Core.Models;

/// <summary>The big numbers at the top of the "Eating habits" section.</summary>
public class ConsumptionSummary
{
    public int MealsLogged { get; init; }

    /// <summary>Number of different days on which at least one meal was logged.</summary>
    public int DaysLogged { get; init; }

    public double TotalCalories { get; init; }

    /// <summary>Average calories on the days that have at least one logged meal (days without logging are not counted).</summary>
    public double AverageDailyCalories { get; init; }

    public double AverageCaloriesPerMeal { get; init; }
}
