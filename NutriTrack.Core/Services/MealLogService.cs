using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// The meal log: everything the user ate (MealLog.txt). It works exactly like PantryService and ReceiptService:
/// one shared instance (see AppServices), TXT file next to the .exe, LastSaveError instead of silent failures.
/// </summary>
public class MealLogService
{
    private readonly string _filePath;
    private readonly List<MealLogEntry> _entries = new();
    /// <summary>Message of the last failed save, or null when the last save worked.</summary>
    public string? LastSaveError { get; private set; }

    /// <summary>True on the very first run (no MealLog.txt yet): the app then creates a few weeks of demo history.</summary>
    public bool NeedsDemoData { get; private set; }

    public MealLogService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MealLog.txt");
        Load();
    }

    /// <summary>Everything that was eaten, newest first.</summary>
    public List<MealLogEntry> GetAll() => _entries.OrderByDescending(e => e.EatenAt).ToList();

    /// <summary>Logs "I ate this recipe" and copies the nutrition values (per serving x servings) into the entry.</summary>
    public MealLogEntry AddFromRecipe(Recipe recipe, MealType type, int servings = 1, DateTime? eatenAt = null)
    {
        var entry = MakeEntry(recipe, type, servings, eatenAt ?? DateTime.Now);
        entry.EntryId = NextId();
        _entries.Add(entry);
        Save();
        return entry;
    }

    public bool Delete(int entryId)
    {
        var existing = _entries.FirstOrDefault(e => e.EntryId == entryId);
        if (existing == null) return false;

        _entries.Remove(existing);
        Save();
        return true;
    }

    private static MealLogEntry MakeEntry(Recipe recipe, MealType type, int servings, DateTime eatenAt)
    {
        servings = Math.Max(1, servings);

        return new MealLogEntry
        {
            RecipeId = recipe.RecipeId,
            RecipeName = recipe.Name,
            Cuisine = recipe.Cuisine,
            Category = recipe.Category,
            MealType = type,
            EatenAt = eatenAt,
            Servings = servings,
            Calories = recipe.CaloriesPerServing * servings,
            Protein = recipe.ProteinPerServing * servings,
            Carbs = recipe.CarbsPerServing * servings,
            Fat = recipe.FatPerServing * servings
        };
    }

    public void Load()
    {
        _entries.Clear();
        NeedsDemoData = false;
        try
        {
            if (File.Exists(_filePath))
            {
                foreach (var line in File.ReadAllLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var f = TextFile.SplitRecord(line);
                    if (f.Length != 13 || f[0] != "M") continue;
                    _entries.Add(new MealLogEntry
                    {
                        EntryId = int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture),
                        RecipeId = int.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                        RecipeName = TextFile.DecodeField(f[3]),
                        Cuisine = TextFile.DecodeField(f[4]),
                        Category = TextFile.DecodeField(f[5]),
                        MealType = Enum.Parse<MealType>(f[6], true),
                        EatenAt = DateTime.Parse(f[7], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                        Servings = int.Parse(f[8], System.Globalization.CultureInfo.InvariantCulture),
                        Calories = double.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture),
                        Protein = string.IsNullOrEmpty(f[10]) ? null : double.Parse(f[10], System.Globalization.CultureInfo.InvariantCulture),
                        Carbs = string.IsNullOrEmpty(f[11]) ? null : double.Parse(f[11], System.Globalization.CultureInfo.InvariantCulture),
                        Fat = string.IsNullOrEmpty(f[12]) ? null : double.Parse(f[12], System.Globalization.CultureInfo.InvariantCulture)
                    });
                }
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            _entries.Clear();
            LastSaveError = ex.Message;
            return;
        }

        NeedsDemoData = true;
    }

    public void Save()
    {
        try
        {
            var lines = new List<string>
            {
                "# NutriTrack meal history | M|entryId|recipeId|recipeName(base64)|cuisine(base64)|category(base64)|mealType|eatenAt(ISO)|servings|calories|protein|carbs|fat"
            };
            lines.AddRange(_entries.Select(e => string.Join("|", "M",
                e.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                e.RecipeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TextFile.EncodeField(e.RecipeName),
                TextFile.EncodeField(e.Cuisine),
                TextFile.EncodeField(e.Category),
                e.MealType.ToString(),
                e.EatenAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                e.Servings.ToString(System.Globalization.CultureInfo.InvariantCulture),
                e.Calories.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                e.Protein?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                e.Carbs?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                e.Fat?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));
            TextFile.WriteAtomic(_filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }
    }

    private int NextId() => _entries.Count > 0 ? _entries.Max(e => e.EntryId) + 1 : 1;

    // Demo data (first run only), so the Analytics charts are not empty during a demo

    private static readonly (MealType Type, int Hour, int Minute, double Chance)[] DemoSlots =
    {
        (MealType.Breakfast, 8, 0, 0.85),
        (MealType.Lunch, 12, 30, 0.90),
        (MealType.Dinner, 19, 0, 0.90),
        (MealType.Snack, 16, 0, 0.60)
    };

    /// <summary>Fills the log with about 3 weeks of plausible meals taken from the real recipes. Same result on every run (fixed seed).</summary>
    public void SeedDemoData(IEnumerable<Recipe> recipes)
    {
        NeedsDemoData = false;

        var usable = recipes.Where(r => r.CaloriesPerServing > 0).ToList();
        if (usable.Count == 0) return;

        var random = new Random(2026);
        int nextId = NextId();

        for (int daysAgo = 21; daysAgo >= 1; daysAgo--)
        {
            DateTime day = DateTime.Today.AddDays(-daysAgo);

            foreach (var (type, hour, minute, chance) in DemoSlots)
            {
                if (random.NextDouble() > chance) continue;   // sometimes a meal is skipped

                var pool = usable
                    .Where(r => MenuRecommender.CategoriesFor(type).Contains(r.Category, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (pool.Count == 0) continue;

                // 40% of the time a "regular" (one of the first 3), otherwise anything: some recipes get eaten more often.
                var recipe = random.NextDouble() < 0.4
                    ? pool[random.Next(Math.Min(3, pool.Count))]
                    : pool[random.Next(pool.Count)];

                var entry = MakeEntry(recipe, type, servings: 1, eatenAt: day.AddHours(hour).AddMinutes(minute));
                entry.EntryId = nextId++;
                _entries.Add(entry);
            }
        }

        Save();
    }
}
