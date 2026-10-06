namespace NutriTrack.Core.Models;

public enum AvailabilityStatus
{
    /// <summary>Qualitative ingredient ("salt to taste"): there is no amount to compare.</summary>
    NoQuantity,
    /// <summary>The ingredient could not be linked to a product, so the pantry cannot know about it.</summary>
    NotTracked,
    /// <summary>Known product, none of it in the pantry.</summary>
    Missing,
    /// <summary>Some of it is in the pantry, but not enough.</summary>
    Partial,
    /// <summary>Pantry covers the required amount.</summary>
    Available,
    /// <summary>The pantry holds the product in an incomparable unit (e.g. pieces vs grams).</summary>
    UnitMismatch
}

/// <summary>Result of checking one scaled ingredient against the pantry.</summary>
public class IngredientAvailability
{
    public required ScaledIngredient Ingredient { get; init; }
    public AvailabilityStatus Status { get; init; }
    public PantryItem? PantryItem { get; init; }

    /// <summary>Amount still missing, expressed in the ingredient's own unit. 0 when the pantry covers it.</summary>
    public double MissingQuantity { get; init; }

    /// <summary>Text for the "Required / Pantry / Missing" line under the ingredient.</summary>
    public string DetailLine { get; init; } = string.Empty;

    /// <summary>True when something has to be bought for this ingredient.</summary>
    public bool NeedsPurchase => Status is AvailabilityStatus.Missing or AvailabilityStatus.Partial
                                           or AvailabilityStatus.NotTracked or AvailabilityStatus.UnitMismatch;

    /// <summary>What goes on the shopping list: only the shortfall for Partial, the full required amount otherwise.</summary>
    public double QuantityToBuy => Status == AvailabilityStatus.Partial ? MissingQuantity : Ingredient.Quantity;

    public string StatusSymbol => Status switch
    {
        AvailabilityStatus.Available => "✓",
        AvailabilityStatus.Partial => "◐",
        AvailabilityStatus.Missing => "✕",
        AvailabilityStatus.UnitMismatch => "?",
        AvailabilityStatus.NotTracked => "•",
        _ => "–"
    };

    public bool HasDetail => DetailLine.Length > 0;
}
