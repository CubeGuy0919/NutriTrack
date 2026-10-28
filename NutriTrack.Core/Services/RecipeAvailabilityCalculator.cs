using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Compares the scaled ingredients of a recipe with the pantry, using the structured ingredient data
/// (ProductId + quantity + unit). Because it receives the SCALED ingredients, "required" always reflects the selected servings.
/// Amounts are compared in grams / millilitres / counts, so 1 kg in the pantry covers 750 g, and only the wording of the
/// result follows the chosen unit system.
/// </summary>
public static class RecipeAvailabilityCalculator
{
    private const double Epsilon = 1e-6;

    // Pantry rows with their still-unused amount, so a product used by two ingredient lines is not counted twice
    private sealed class Stock
    {
        public required PantryItem Item { get; init; }
        public required BaseAmount Amount { get; init; }
        public double Remaining { get; set; }
    }

    public static IReadOnlyList<IngredientAvailability> Check(
        IEnumerable<ScaledIngredient> ingredients,
        IEnumerable<PantryItem> pantry,
        UnitSystem system = UnitSystem.AsWritten)
    {
        var stock = pantry
            .Where(p => p.Quantity > 0)
            .Select(p =>
            {
                var amount = UnitConverter.ToBase(p.Quantity, p.Unit);
                return new Stock { Item = p, Amount = amount, Remaining = amount.Value };
            })
            .ToList();

        return ingredients.Select(i => CheckOne(i, stock, system)).ToList();
    }

    private static IngredientAvailability CheckOne(ScaledIngredient ingredient, List<Stock> stock, UnitSystem system)
    {
        if (!ingredient.IsScalable)
            return new IngredientAvailability { Ingredient = ingredient, Status = AvailabilityStatus.NoQuantity };

        if (!ingredient.ProductId.HasValue)
        {
            return new IngredientAvailability
            {
                Ingredient = ingredient,
                Status = AvailabilityStatus.NotTracked,
                MissingQuantity = ingredient.Quantity,
                DetailLine = "Not in the product list, so the pantry can't be checked"
            };
        }

        string unit = ingredient.Source.Unit;
        var required = UnitConverter.ToBase(ingredient.Quantity, unit);
        string requiredLabel = ingredient.QuantityLabel;

        var rows = stock.Where(s => s.Item.ProductId == ingredient.ProductId.Value).ToList();
        if (rows.Count == 0)
        {
            return new IngredientAvailability
            {
                Ingredient = ingredient,
                Status = AvailabilityStatus.Missing,
                MissingQuantity = ingredient.Quantity,
                DetailLine = $"Required: {requiredLabel} · Not in pantry"
            };
        }

        var compatible = rows.Where(s => UnitConverter.AreCompatible(required, s.Amount)).ToList();
        if (compatible.Count == 0)
        {
            return new IngredientAvailability
            {
                Ingredient = ingredient,
                Status = AvailabilityStatus.UnitMismatch,
                PantryItem = rows[0].Item,
                MissingQuantity = ingredient.Quantity,
                DetailLine = $"Required: {requiredLabel} · Pantry: {rows[0].Item.FormattedQuantity} (different units, can't compare)"
            };
        }

        double have = compatible.Sum(s => s.Remaining);
        string haveLabel = QuantityFormatter.FormatAmount(have, required.BaseUnit, system);

        // Use up what this ingredient takes from the pantry
        double toTake = Math.Min(have, required.Value);
        foreach (var row in compatible)
        {
            double take = Math.Min(row.Remaining, toTake);
            row.Remaining -= take;
            toTake -= take;
            if (toTake <= Epsilon) break;
        }

        double missingBase = required.Value - have;
        if (missingBase <= Epsilon)
        {
            return new IngredientAvailability
            {
                Ingredient = ingredient,
                Status = AvailabilityStatus.Available,
                PantryItem = compatible[0].Item,
                DetailLine = $"Required: {requiredLabel} · Pantry: {haveLabel}"
            };
        }

        if (have <= Epsilon)
        {
            return new IngredientAvailability
            {
                Ingredient = ingredient,
                Status = AvailabilityStatus.Missing,
                PantryItem = compatible[0].Item,
                MissingQuantity = ingredient.Quantity,
                DetailLine = $"Required: {requiredLabel} · Not in pantry"
            };
        }

        // Shortfall in the recipe's own unit (what goes on the shopping list)
        double missing = Math.Round(UnitConverter.FromBase(missingBase, unit), 2, MidpointRounding.AwayFromZero);
        string missingLabel = QuantityFormatter.FormatAmount(missing, unit, system);

        return new IngredientAvailability
        {
            Ingredient = ingredient,
            Status = AvailabilityStatus.Partial,
            PantryItem = compatible[0].Item,
            MissingQuantity = missing,
            DetailLine = $"Required: {requiredLabel} · Pantry: {haveLabel} · Missing: {missingLabel}"
        };
    }
}
