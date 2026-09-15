namespace NutriTrack.Core.Models;

/// <summary>
/// A single food product in the shared food database.
/// Nutrition values are expressed per 100g/100ml.
/// </summary>
public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string Unit { get; set; } = "g";

    public double CaloriesPer100 { get; set; }
    public double ProteinPer100 { get; set; }
    public double CarbsPer100 { get; set; }
    public double FatPer100 { get; set; }
    public double FiberPer100 { get; set; }
    public double SugarPer100 { get; set; }
    public double SaltPer100 { get; set; }

    public override string ToString() => Name;
}
