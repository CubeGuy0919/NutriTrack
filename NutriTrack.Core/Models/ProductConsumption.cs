namespace NutriTrack.Core.Models;

public class ProductConsumption
{
    public int ConsumptionId { get; set; }
    public int UserId { get; set; }
    public Product Product { get; set; } = new();

    /// <summary>Amount consumed in grams or milliliters.</summary>
    public double AmountConsumed { get; set; }
    public DateTime ConsumedAt { get; set; } = DateTime.Now;

    // Direct calculated nutritional yields based on portion size
    public double TotalCalories => (Product.CaloriesPer100 / 100.0) * AmountConsumed;
    public double TotalProtein => (Product.ProteinPer100 / 100.0) * AmountConsumed;
    public double TotalCarbs => (Product.CarbsPer100 / 100.0) * AmountConsumed;
    public double TotalFat => (Product.FatPer100 / 100.0) * AmountConsumed;
    public double TotalFiber => (Product.FiberPer100 / 100.0) * AmountConsumed;
    public double TotalSugar => (Product.SugarPer100 / 100.0) * AmountConsumed;
    public double TotalSalt => (Product.SaltPer100 / 100.0) * AmountConsumed;
}