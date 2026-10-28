namespace NutriTrack.Core.Models;

public class ShoppingListItem
{
    /// <summary>Product id when the ingredient is a known product; null for free-text ingredients.</summary>
    public int? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    public bool HasProduct => ProductId.HasValue;

    public string QuantityLabel =>
        $"{NutriTrack.Core.Services.QuantityFormatter.Format(Quantity, Unit)} {NutriTrack.Core.Services.UnitConverter.DisplayUnit(Unit, Quantity)}".Trim();
}
