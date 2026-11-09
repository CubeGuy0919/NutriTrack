using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// The pantry: what is at home, how much, and until when (Pantry.txt).
/// One shared instance (see AppServices) is the single source of truth for the Pantry screen, the Dashboard and the
/// recipe detail panel. Ingredient matching uses the structured recipe data (ProductId + quantity + unit).
/// </summary>
public class PantryService
{
    private readonly string _filePath;
    private readonly List<PantryItem> _pantryItems = new();
    /// <summary>Message of the last failed save, or null when the last save worked. Views show it instead of failing silently.</summary>
    public string? LastSaveError { get; private set; }

    public PantryService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pantry.txt");
        LoadPantry();
    }

    public List<PantryItem> GetAllPantryItems() => _pantryItems.OrderBy(p => p.ExpiryDate ?? DateTime.MaxValue).ToList();

    public PantryItem? GetItemById(int id) => _pantryItems.FirstOrDefault(p => p.PantryItemId == id);

    public List<PantryItem> GetExpiringSoonItems(int withinDays = 3)
    {
        var today = DateTime.Today;
        return _pantryItems
            .Where(p => p.ExpiryDate.HasValue &&
                        p.ExpiryDate.Value.Date >= today &&
                        (p.ExpiryDate.Value.Date - today).TotalDays <= withinDays)
            .OrderBy(p => p.ExpiryDate!.Value)
            .ToList();
    }

    /// <summary>
    /// Recipes of which at least one ingredient is in the pantry, best match first. An ingredient counts as covered when
    /// it is linked to a product (ProductId) that the pantry holds. Ingredients that could not be linked to a product
    /// count towards the total but can never be matched.
    /// </summary>
    public List<RecipeMatchResult> GetAvailableRecipes(IEnumerable<Recipe> allRecipes)
    {
        var inPantry = _pantryItems
            .Where(p => p.ProductId > 0 && p.Quantity > 0)
            .Select(p => p.ProductId)
            .ToHashSet();

        var results = new List<RecipeMatchResult>();

        foreach (var recipe in allRecipes)
        {
            var matched = new List<string>();
            var missing = new List<string>();

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.ProductId.HasValue && inPantry.Contains(ingredient.ProductId.Value))
                    matched.Add(ingredient.DisplayText);
                else
                    missing.Add(ingredient.DisplayText);
            }

            if (matched.Count == 0) continue;

            results.Add(new RecipeMatchResult
            {
                Recipe = recipe,
                MatchedIngredientsCount = matched.Count,
                TotalIngredientsCount = recipe.Ingredients.Count,
                MatchedIngredients = matched,
                MissingIngredients = missing
            });
        }

        return results
            .OrderByDescending(r => r.MatchPercentage)
            .ThenByDescending(r => r.MatchedIngredientsCount)
            .ToList();
    }

    /// <summary>A recipe that would use up the given pantry item (the one the pantry covers best), or null.</summary>
    public Recipe? FindSuggestedRecipeForPantryItem(PantryItem item, IEnumerable<Recipe> allRecipes)
    {
        var recipes = allRecipes.ToList();

        if (item.ProductId > 0)
        {
            var inPantry = _pantryItems.Where(p => p.ProductId > 0).Select(p => p.ProductId).ToHashSet();

            var best = recipes
                .Where(r => r.Ingredients.Any(i => i.ProductId == item.ProductId))
                .OrderByDescending(r => r.Ingredients.Count(i => i.ProductId.HasValue && inPantry.Contains(i.ProductId.Value)))
                .ThenBy(r => r.TotalTimeMinutes)
                .FirstOrDefault();

            if (best != null) return best;
        }

        if (string.IsNullOrWhiteSpace(item.ProductName)) return null;
        string name = item.ProductName.Trim();

        var byName = recipes.FirstOrDefault(r =>
            r.Ingredients.Any(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        if (byName != null) return byName;

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
        if (words.Count == 0) return null;

        return recipes.FirstOrDefault(r =>
            r.Ingredients.Any(i => words.Any(w => i.Name.Contains(w, StringComparison.OrdinalIgnoreCase))));
    }

    public void AddPantryItem(PantryItem item)
    {
        if (item.PantryItemId <= 0)
            item.PantryItemId = NextId();

        _pantryItems.Add(item);
        SavePantry();
    }

    public void UpdatePantryItem(PantryItem item)
    {
        var existing = _pantryItems.FirstOrDefault(p => p.PantryItemId == item.PantryItemId);
        if (existing == null) return;

        existing.ProductId = item.ProductId;
        existing.ProductName = item.ProductName;
        existing.Category = item.Category;
        existing.Quantity = item.Quantity;
        existing.Unit = item.Unit;
        existing.ExpiryDate = item.ExpiryDate;
        SavePantry();
    }

    public bool DeletePantryItem(int pantryItemId)
    {
        var existing = _pantryItems.FirstOrDefault(p => p.PantryItemId == pantryItemId);
        if (existing == null) return false;

        _pantryItems.Remove(existing);
        SavePantry();
        return true;
    }

    /// <summary>
    /// Adds stock. When the pantry already holds the same product with a compatible unit and the same expiry date,
    /// that entry is increased (1 kg added to 500 g gives 1500 g); otherwise a new entry is created.
    /// Returns false when the quantity is not a positive number.
    /// </summary>
    public bool AddOrIncrease(int productId, string name, string? category, double quantity, string? unit, DateTime? expiryDate = null)
    {
        if (!(quantity > 0) || double.IsInfinity(quantity)) return false;

        var added = UnitConverter.ToBase(quantity, unit);

        var existing = productId > 0
            ? _pantryItems.FirstOrDefault(p =>
                p.ProductId == productId &&
                p.ExpiryDate?.Date == expiryDate?.Date &&
                UnitConverter.AreCompatible(UnitConverter.ToBase(p.Quantity, p.Unit), added))
            : null;

        if (existing != null)
        {
            existing.Quantity = Math.Round(existing.Quantity + UnitConverter.FromBase(added.Value, existing.Unit), 2);
            SavePantry();
            return true;
        }

        AddPantryItem(new PantryItem
        {
            ProductId = productId,
            ProductName = name,
            Category = category,
            Quantity = Math.Round(quantity, 2),
            Unit = string.IsNullOrWhiteSpace(unit) ? added.BaseUnit : unit.Trim(),
            ExpiryDate = expiryDate
        });
        return true;
    }

    public void LoadPantry()
    {
        _pantryItems.Clear();
        try
        {
            if (File.Exists(_filePath))
            {
                foreach (var line in File.ReadAllLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var f = TextFile.SplitRecord(line);
                    if (f.Length != 8 || f[0] != "P") continue;
                    _pantryItems.Add(new PantryItem
                    {
                        PantryItemId = int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture),
                        ProductId = int.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                        ProductName = TextFile.DecodeField(f[3]),
                        Category = string.IsNullOrEmpty(f[4]) ? null : TextFile.DecodeField(f[4]),
                        Quantity = double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture),
                        Unit = TextFile.DecodeField(f[6]),
                        ExpiryDate = TextFile.ParseNullableDate(f[7])
                    });
                }
                if (_pantryItems.Count > 0) return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            _pantryItems.Clear();
            LastSaveError = ex.Message;
        }
        SeedInitialPantry();
        SavePantry();
    }

    public void SavePantry()
    {
        try
        {
            var lines = new List<string> { "# NutriTrack pantry data | P|id|productId|name(base64)|category(base64)|quantity|unit(base64)|expiry(ISO)" };
            lines.AddRange(_pantryItems.Select(p => string.Join("|", "P",
                p.PantryItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                p.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TextFile.EncodeField(p.ProductName),
                TextFile.EncodeField(p.Category),
                p.Quantity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                TextFile.EncodeField(p.Unit),
                p.ExpiryDate?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));
            TextFile.WriteAtomic(_filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }
    }

    private int NextId() => _pantryItems.Count > 0 ? _pantryItems.Max(p => p.PantryItemId) + 1 : 1;

    private void SeedInitialPantry()
    {
        var today = DateTime.Today;

        // A varied first-run pantry gives the demo useful examples of fresh, expiring and expired stock.
        _pantryItems.AddRange(new[]
        {
            new PantryItem { PantryItemId = 1, ProductId = 4, ProductName = "Whole Milk", Category = "Dairy", Quantity = 1.5, Unit = "L", ExpiryDate = today.AddDays(2) },
            new PantryItem { PantryItemId = 2, ProductId = 7, ProductName = "Eggs", Category = "Dairy", Quantity = 6, Unit = "pcs", ExpiryDate = today.AddDays(1) },
            new PantryItem { PantryItemId = 3, ProductId = 1, ProductName = "Chicken Breast", Category = "Meat", Quantity = 800, Unit = "g", ExpiryDate = today.AddDays(5) },
            new PantryItem { PantryItemId = 4, ProductId = 8, ProductName = "White Rice (Uncooked)", Category = "Grains", Quantity = 1000, Unit = "g", ExpiryDate = today.AddDays(180) },
            new PantryItem { PantryItemId = 5, ProductId = 6, ProductName = "Cheddar Cheese", Category = "Dairy", Quantity = 200, Unit = "g", ExpiryDate = today.AddDays(-1) },
            new PantryItem { PantryItemId = 6, ProductId = 13, ProductName = "Banana", Category = "Produce", Quantity = 5, Unit = "pcs", ExpiryDate = today.AddDays(2) },
            new PantryItem { PantryItemId = 7, ProductId = 14, ProductName = "Apple", Category = "Produce", Quantity = 4, Unit = "pcs", ExpiryDate = today.AddDays(8) },
            new PantryItem { PantryItemId = 8, ProductId = 15, ProductName = "Broccoli", Category = "Produce", Quantity = 350, Unit = "g", ExpiryDate = today.AddDays(3) },
            new PantryItem { PantryItemId = 9, ProductId = 5, ProductName = "Greek Yogurt (Plain)", Category = "Dairy", Quantity = 400, Unit = "g", ExpiryDate = today.AddDays(6) },
            new PantryItem { PantryItemId = 10, ProductId = 9, ProductName = "Oats", Category = "Grains", Quantity = 500, Unit = "g", ExpiryDate = today.AddDays(120) },
            new PantryItem { PantryItemId = 11, ProductId = 11, ProductName = "Olive Oil", Category = "Oils", Quantity = 250, Unit = "ml", ExpiryDate = null },
            new PantryItem { PantryItemId = 12, ProductId = 17, ProductName = "Potatoes", Category = "Produce", Quantity = 1.2, Unit = "kg", ExpiryDate = today.AddDays(14) },
            new PantryItem { PantryItemId = 13, ProductId = 16, ProductName = "Spinach", Category = "Produce", Quantity = 150, Unit = "g", ExpiryDate = today.AddDays(-2) }
        });
    }
}
