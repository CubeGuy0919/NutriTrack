using System.Globalization;
using System.Text.Json;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// The pantry: what is at home, how much, and until when (Pantry.json).
/// One shared instance (see AppServices) is the single source of truth for the Pantry screen, the Dashboard and the
/// recipe detail panel. Ingredient matching uses the structured recipe data (ProductId + quantity + unit).
/// </summary>
public class PantryService
{
    private readonly string _filePath;
    private readonly List<PantryItem> _pantryItems = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Message of the last failed save, or null when the last save worked. Views show it instead of failing silently.</summary>
    public string? LastSaveError { get; private set; }

    public PantryService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pantry.json");
        LoadPantry();
    }

    // ---------------------------------------------------------------
    // Queries
    // ---------------------------------------------------------------

    public List<PantryItem> GetAllPantryItems() => _pantryItems.OrderBy(p => p.ExpiryDate ?? DateTime.MaxValue).ToList();

    public PantryItem? GetItemById(int id) => _pantryItems.FirstOrDefault(p => p.PantryItemId == id);

    public List<PantryItem> GetExpiringSoonItems(int withinDays = 3)
    {
        var today = DateTime.Today;
        return _pantryItems
            .Where(p => p.ExpiryDate.HasValue && (p.ExpiryDate.Value.Date - today).TotalDays <= withinDays)
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

        // Free-text pantry item (no product): look for its name in the ingredient names
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

    // ---------------------------------------------------------------
    // Changes
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // Persistence
    // ---------------------------------------------------------------

    public void LoadPantry()
    {
        _pantryItems.Clear();

        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                var items = JsonSerializer.Deserialize<List<PantryItem>>(json, _jsonOptions);
                if (items != null)
                {
                    _pantryItems.AddRange(items);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable file: start again from the legacy file / demo data below (the broken file is kept as .bak on the next save)
        }

        // A simple Pantry.txt (ProductId,Name,Quantity,Unit) from the earlier version is imported once
        if (TryImportLegacyTextFile()) return;

        SeedInitialPantry();
        SavePantry();
    }

    public void SavePantry()
    {
        try
        {
            string json = JsonSerializer.Serialize(_pantryItems, _jsonOptions);
            TextFile.WriteAtomic(_filePath, json);
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }
    }

    private int NextId() => _pantryItems.Count > 0 ? _pantryItems.Max(p => p.PantryItemId) + 1 : 1;

    private bool TryImportLegacyTextFile()
    {
        string legacyPath = Path.ChangeExtension(_filePath, ".txt");

        try
        {
            if (!File.Exists(legacyPath)) return false;

            foreach (var line in File.ReadAllLines(legacyPath).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int productId)) continue;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double quantity) || quantity <= 0) continue;

                _pantryItems.Add(new PantryItem
                {
                    PantryItemId = NextId(),
                    ProductId = productId,
                    ProductName = TextFile.Unescape(parts[1]),
                    Quantity = quantity,
                    Unit = parts[3].Trim()
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (_pantryItems.Count == 0) return false;

        SavePantry();
        return true;
    }

    private void SeedInitialPantry()
    {
        var today = DateTime.Today;

        _pantryItems.Add(new PantryItem { PantryItemId = 1, ProductId = 4, ProductName = "Whole Milk", Category = "Dairy", Quantity = 1.5, Unit = "L", ExpiryDate = today.AddDays(2) });
        _pantryItems.Add(new PantryItem { PantryItemId = 2, ProductId = 7, ProductName = "Eggs", Category = "Dairy", Quantity = 6, Unit = "pcs", ExpiryDate = today.AddDays(1) });
        _pantryItems.Add(new PantryItem { PantryItemId = 3, ProductId = 1, ProductName = "Chicken Breast", Category = "Meat", Quantity = 800, Unit = "g", ExpiryDate = today.AddDays(5) });
        _pantryItems.Add(new PantryItem { PantryItemId = 4, ProductId = 8, ProductName = "White Rice (Uncooked)", Category = "Grains", Quantity = 1000, Unit = "g", ExpiryDate = today.AddDays(180) });
        _pantryItems.Add(new PantryItem { PantryItemId = 5, ProductId = 6, ProductName = "Cheddar Cheese", Category = "Dairy", Quantity = 200, Unit = "g", ExpiryDate = today.AddDays(-1) });
    }
}
