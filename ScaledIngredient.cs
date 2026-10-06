namespace NutriTrack.Core.Models;

/// <summary>
/// One ingredient after serving scaling, ready for display. The original <see cref="IngredientItem"/> is never modified.
/// Qualitative ingredients ("salt to taste") are carried through unchanged with IsScalable = false.
/// </summary>
public class ScaledIngredient
{
    public ScaledIngredient(IngredientItem source, double quantity, string unit, string displayText, string quantityLabel)
    {
        Source = source;
        Quantity = quantity;
        Unit = unit;
        DisplayText = displayText;
        QuantityLabel = quantityLabel;
    }

    public IngredientItem Source { get; }

    public string Name => Source.Name;
    public int? ProductId => Source.ProductId;
    public bool IsScalable => Source.IsScalable;

    /// <summary>Scaled amount (selected servings / base servings x original). 0 when the ingredient is not scalable.</summary>
    public double Quantity { get; }

    /// <summary>Unit used for display (singular/plural adjusted), e.g. "g", "clove", "cloves".</summary>
    public string Unit { get; }

    /// <summary>Just the amount, e.g. "750 g" or "1 1/2 tbsp". Empty when not scalable.</summary>
    public string QuantityLabel { get; }

    /// <summary>Full line, e.g. "750 g chicken breast". For non-scalable ingredients this is the original text, unchanged.</summary>
    public string DisplayText { get; }

    public override string ToString() => DisplayText;
}
