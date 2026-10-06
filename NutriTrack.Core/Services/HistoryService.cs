using System.Text.Json;
using System.Text.Json.Serialization;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Persistent consumption, cooking and waste history (History.json).
/// Replaces the in-memory-only log in <see cref="NutritionAnalyticsService"/>.
/// </summary>
public class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private HistoryData _data = new();

    /// <summary>Raised after any successful change so screens can refresh.</summary>
    public event Action? Changed;

    public string? LastSaveError { get; private set; }

    /// <summary>Set when History.json existed but could not be read; the broken file is kept as .corrupt.</summary>
    public string? LoadWarning { get; private set; }

    public HistoryService(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public IReadOnlyList<ConsumptionEvent> Consumption => _data.Consumption;
    public IReadOnlyList<CookingEvent> Cooking => _data.Cooking;
    public IReadOnlyList<WasteEvent> Waste => _data.Waste;

    // ---- Recording ----

    /// <summary>Logs eating <paramref name="amount"/> g/ml of a product; nutrition is copied from the product now.</summary>
    public ConsumptionEvent? LogConsumption(Product product, double amount, DateTime when,
        string? meal = null, int? recipeId = null, string? recipeName = null)
    {
        if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return null;

        double f = amount / 100.0;
        var e = new ConsumptionEvent
        {
            EventId = NextId(_data.Consumption.Select(c => c.EventId)),
            Timestamp = when,
            ProductId = product.ProductId,
            ProductName = product.Name,
            Category = product.Category,
            Quantity = amount,
            Unit = string.IsNullOrWhiteSpace(product.Unit) ? "g" : product.Unit,
            Calories = product.CaloriesPer100 * f,
            Protein = product.ProteinPer100 * f,
            Carbs = product.CarbsPer100 * f,
            Fat = product.FatPer100 * f,
            Fiber = product.FiberPer100 * f,
            Sugar = product.SugarPer100 * f,
            Salt = product.SaltPer100 * f,
            Meal = meal,
            RecipeId = recipeId,
            RecipeName = recipeName
        };

        _data.Consumption.Add(e);
        Save();
        return e;
    }

    /// <summary>
    /// Saves a cooking event. When <paramref name="logNutrition"/> is true and the recipe has calories on file,
    /// one consumption event named after the recipe is added for the servings eaten.
    /// </summary>
    public CookingEvent RecordCooking(Recipe recipe, int servings, DateTime when, bool logNutrition,
        IEnumerable<PantryDeduction>? deductions = null, IEnumerable<string>? unmatched = null, string? meal = null)
    {
        servings = Math.Max(1, servings);

        var e = new CookingEvent
        {
            EventId = NextId(_data.Cooking.Select(c => c.EventId)),
            Timestamp = when,
            RecipeId = recipe.RecipeId,
            RecipeName = recipe.Name,
            Cuisine = recipe.Cuisine,
            Category = recipe.Category,
            Difficulty = recipe.Difficulty,
            TotalTimeMinutes = recipe.TotalTimeMinutes,
            Servings = servings,
            Calories = recipe.CaloriesPerServing * servings,
            Protein = recipe.ProteinPerServing * servings,
            Carbs = recipe.CarbsPerServing * servings,
            Fat = recipe.FatPerServing * servings,
            NutritionLogged = logNutrition && recipe.CaloriesPerServing > 0,
            PantryDeductions = deductions?.ToList() ?? new(),
            UnmatchedIngredients = unmatched?.ToList() ?? new()
        };

        _data.Cooking.Add(e);

        if (e.NutritionLogged)
        {
            _data.Consumption.Add(new ConsumptionEvent
            {
                EventId = NextId(_data.Consumption.Select(c => c.EventId)),
                Timestamp = when,
                ProductName = recipe.Name,
                Category = recipe.Category,
                Quantity = servings,
                Unit = servings == 1 ? "serving" : "servings",
                Calories = e.Calories,
                Protein = e.Protein ?? 0,
                Carbs = e.Carbs ?? 0,
                Fat = e.Fat ?? 0,
                RecipeId = recipe.RecipeId,
                RecipeName = recipe.Name,
                Meal = meal
            });
        }

        Save();
        return e;
    }

    public WasteEvent RecordWaste(PantryItem item, WasteReason reason, DateTime? when = null,
        double? quantity = null, decimal? estimatedValue = null)
    {
        var e = new WasteEvent
        {
            EventId = NextId(_data.Waste.Select(w => w.EventId)),
            Timestamp = when ?? DateTime.Now,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Category = item.Category,
            Quantity = quantity ?? item.Quantity,
            Unit = item.Unit,
            Reason = reason,
            EstimatedValue = estimatedValue
        };

        _data.Waste.Add(e);
        Save();
        return e;
    }

    public bool DeleteConsumption(int eventId)
    {
        int removed = _data.Consumption.RemoveAll(c => c.EventId == eventId);
        if (removed > 0) Save();
        return removed > 0;
    }

    // ---- Persistence ----

    /// <summary>Next free id: one higher than the biggest id in use (1 when the list is empty).</summary>
    private static int NextId(IEnumerable<int> ids)
    {
        int biggest = 0;
        foreach (int id in ids)
        {
            if (id > biggest) biggest = id;
        }
        return biggest + 1;
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var loaded = JsonSerializer.Deserialize<HistoryData>(File.ReadAllText(_filePath), JsonOptions);
            if (loaded != null)
            {
                loaded.Consumption ??= new();
                loaded.Cooking ??= new();
                loaded.Waste ??= new();
                _data = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadWarning = "History could not be read and was started empty: " + ex.Message;
            try { File.Copy(_filePath, _filePath + ".corrupt", overwrite: true); } catch (IOException) { }
            _data = new HistoryData();
        }
    }

    private void Save()
    {
        try
        {
            TextFile.WriteAtomic(_filePath, JsonSerializer.Serialize(_data, JsonOptions));
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }

        Changed?.Invoke();
    }
}
