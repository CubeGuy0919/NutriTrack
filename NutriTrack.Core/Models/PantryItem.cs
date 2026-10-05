namespace NutriTrack.Core.Models;

/// <summary>
/// Stock of one product. Mass is always stored in grams, volume in millilitres and anything countable
/// under its count unit ("pcs", "clove" ...), so comparing with a recipe never depends on how the amount was typed.
/// </summary>
public class PantryItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Unit { get; set; } = "g";

    public string QuantityLabel => $"{NutriTrack.Core.Services.QuantityFormatter.Format(Quantity, Unit)} {Unit}".Trim();
}
