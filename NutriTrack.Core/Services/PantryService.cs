using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class RecipeMatchResult
{
    public Recipe Recipe { get; set; } = new();
    public int MatchedIngredientsCount { get; set; }
    public int TotalIngredientsCount { get; set; }
    public List<string> MatchedIngredients { get; set; } = new();
    public List<string> MissingIngredients { get; set; } = new();

    public double MatchPercentage => TotalIngredientsCount > 0
        ? (double)MatchedIngredientsCount / TotalIngredientsCount
        : 0;

    public string MatchBadge
    {
        get
        {
            if (MatchedIngredientsCount >= TotalIngredientsCount && TotalIngredientsCount > 0)
                return "✓ All ingredients available";
            if (MatchPercentage >= 0.5)
                return "✓ Most ingredients available";
            return $"{MatchedIngredientsCount}/{TotalIngredientsCount} ingredients";
        }
    }
}

public class PantryService
{
    private readonly string _filePath;
    private readonly List<PantryItem> _pantryItems = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PantryService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pantry.json");
        LoadPantry();
    }

    public List<PantryItem> GetAllPantryItems() => _pantryItems.OrderBy(p => p.ExpiryDate ?? DateTime.MaxValue).ToList();

    public PantryItem? GetItemById(int id) => _pantryItems.FirstOrDefault(p => p.PantryItemId == id);

    public void AddPantryItem(PantryItem item)
    {
        if (item.PantryItemId <= 0)
        {
            item.PantryItemId = _pantryItems.Count > 0 ? _pantryItems.Max(p => p.PantryItemId) + 1 : 1;
        }
        _pantryItems.Add(item);
        SavePantry();
    }

    public void UpdatePantryItem(PantryItem item)
    {
        var existing = _pantryItems.FirstOrDefault(p => p.PantryItemId == item.PantryItemId);
        if (existing != null)
        {
            existing.ProductId = item.ProductId;
            existing.ProductName = item.ProductName;
            existing.Category = item.Category;
            existing.Quantity = item.Quantity;
            existing.Unit = item.Unit;
            existing.ExpiryDate = item.ExpiryDate;
            SavePantry();
        }
    }

    public bool DeletePantryItem(int pantryItemId)
    {
        var existing = _pantryItems.FirstOrDefault(p => p.PantryItemId == pantryItemId);
        if (existing != null)
        {
            _pantryItems.Remove(existing);
            SavePantry();
            return true;
        }
        return false;
    }

    public List<PantryItem> GetExpiringSoonItems(int withinDays = 3)
    {
        var today = DateTime.Today;
        return _pantryItems
            .Where(p => p.ExpiryDate.HasValue && (p.ExpiryDate.Value.Date - today).TotalDays <= withinDays)
            .OrderBy(p => p.ExpiryDate!.Value)
            .ToList();
    }

    public List<RecipeMatchResult> GetAvailableRecipes(IEnumerable<Recipe> allRecipes)
    {
        var results = new List<RecipeMatchResult>();
        var pantryNames = _pantryItems
            .Select(p => p.ProductName.Trim().ToLowerInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        foreach (var recipe in allRecipes)
        {
            int matched = 0;
            var matchedList = new List<string>();
            var missingList = new List<string>();

            foreach (var ingredient in recipe.Ingredients)
            {
                string ingLower = ingredient.ToLowerInvariant();
                bool isMatched = pantryNames.Any(pName =>
                    ingLower.Contains(pName) ||
                    pName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part.Length > 2 && ingLower.Contains(part))
                );

                if (isMatched)
                {
                    matched++;
                    matchedList.Add(ingredient);
                }
                else
                {
                    missingList.Add(ingredient);
                }
            }

            if (matched > 0)
            {
                results.Add(new RecipeMatchResult
                {
                    Recipe = recipe,
                    MatchedIngredientsCount = matched,
                    TotalIngredientsCount = recipe.Ingredients.Count,
                    MatchedIngredients = matchedList,
                    MissingIngredients = missingList
                });
            }
        }

        return results
            .OrderByDescending(r => r.MatchPercentage)
            .ThenByDescending(r => r.MatchedIngredientsCount)
            .ToList();
    }

    public Recipe? FindSuggestedRecipeForPantryItem(PantryItem item, IEnumerable<Recipe> allRecipes)
    {
        if (string.IsNullOrWhiteSpace(item.ProductName)) return null;
        string name = item.ProductName.Trim().ToLowerInvariant();

        // 1. Direct exact or substring match in ingredients
        var best = allRecipes.FirstOrDefault(r =>
            r.Ingredients.Any(i => i.ToLowerInvariant().Contains(name)));

        if (best != null) return best;

        // 2. Token/word match (e.g. "Chicken" from "Chicken Breast")
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        if (words.Count > 0)
        {
            best = allRecipes.FirstOrDefault(r =>
                r.Ingredients.Any(i =>
                {
                    string iLower = i.ToLowerInvariant();
                    return words.Any(w => iLower.Contains(w));
                }));
        }

        return best;
    }

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
        catch
        {
            // Silently fall back to seed data if corrupted
        }

        SeedInitialPantry();
        SavePantry();
    }

    public void SavePantry()
    {
        try
        {
            string json = JsonSerializer.Serialize(_pantryItems, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silently handle IO errors in restricted environments
        }
    }

    private void SeedInitialPantry()
    {
        var today = DateTime.Today;
        _pantryItems.Add(new PantryItem
        {
            PantryItemId = 1,
            ProductId = 4,
            ProductName = "Whole Milk",
            Category = "Dairy",
            Quantity = 1.5,
            Unit = "L",
            ExpiryDate = today.AddDays(2) // Expiring soon
        });

        _pantryItems.Add(new PantryItem
        {
            PantryItemId = 2,
            ProductId = 7,
            ProductName = "Eggs",
            Category = "Dairy",
            Quantity = 6,
            Unit = "pcs",
            ExpiryDate = today.AddDays(1) // Expiring soon (tomorrow)
        });

        _pantryItems.Add(new PantryItem
        {
            PantryItemId = 3,
            ProductId = 1,
            ProductName = "Chicken Breast",
            Category = "Meat",
            Quantity = 800,
            Unit = "g",
            ExpiryDate = today.AddDays(5) // Fresh
        });

        _pantryItems.Add(new PantryItem
        {
            PantryItemId = 4,
            ProductId = 8,
            ProductName = "White Rice (Uncooked)",
            Category = "Grains",
            Quantity = 1000,
            Unit = "g",
            ExpiryDate = today.AddDays(180) // Long shelf life
        });

        _pantryItems.Add(new PantryItem
        {
            PantryItemId = 5,
            ProductId = 6,
            ProductName = "Cheddar Cheese",
            Category = "Dairy",
            Quantity = 200,
            Unit = "g",
            ExpiryDate = today.AddDays(-1) // Expired yesterday
        });
    }
}
