using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class RecipeService
{
    public List<Recipe> LoadRecipesFromTxt(string filePath)
    {
        var recipes = new List<Recipe>();

        if (!File.Exists(filePath))
            return recipes;

        var lines = File.ReadAllLines(filePath).Skip(1); // Skip CSV header

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', 10);
            if (parts.Length < 10) continue;

            try
            {
                recipes.Add(new Recipe
                {
                    RecipeId = int.TryParse(parts[0], out int id) ? id : 0,
                    Name = parts[1].Trim(),
                    Cuisine = parts[2].Trim(),
                    Category = parts[3].Trim(),
                    PrepTimeMinutes = int.TryParse(parts[4], out int prep) ? prep : 0,
                    CookTimeMinutes = int.TryParse(parts[5], out int cook) ? cook : 0,
                    CaloriesPerServing = double.TryParse(parts[6], out double cal) ? cal : 0,
                    FoodSensitivities = parts[7].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    Ingredients = parts[8].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).ToList(),
                    Instructions = parts[9].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                });
            }
            catch
            {
                // Skip corrupted individual lines gracefully
                continue;
            }
        }

        return recipes;
    }
}