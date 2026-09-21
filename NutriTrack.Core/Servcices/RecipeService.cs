using System.IO;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class RecipeService
{
    public List<Recipe> LoadRecipesFromTxt(string filePath)
    {
        var recipes = new List<Recipe>();

        if (!File.Exists(filePath))
            return recipes;

        var lines = File.ReadAllLines(filePath).Skip(1);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 9) continue;

            recipes.Add(new Recipe
            {
                RecipeId = int.Parse(parts[0]),
                Name = parts[1],
                Cuisine = parts[2],
                Category = parts[3],
                PrepTimeMinutes = int.Parse(parts[4]),
                CookTimeMinutes = int.Parse(parts[5]),
                CaloriesPerServing = double.Parse(parts[6]),
                Ingredients = parts[7].Split(';').ToList(),
                Instructions = parts[8]
            });
        }

        return recipes;
    }
}