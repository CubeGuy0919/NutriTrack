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
        List<Recipe> databaseRecipes = new List<Recipe>();

        if (!File.Exists(filePath))
            return databaseRecipes;

        IEnumerable<string> recipesFileLines = File.ReadAllLines(filePath).Skip(1); // Skip CSV header

        foreach (string? recipeLine in recipesFileLines)
        {
            if (string.IsNullOrWhiteSpace(recipeLine)) continue;

            string[] recipeLineParts = recipeLine.Split(',', 11);
            if (recipeLineParts.Length < 11) continue;

            try
            {
                databaseRecipes.Add(new Recipe(
                    int.TryParse(recipeLineParts[0], out int id) ? id : 0,
                    recipeLineParts[1].Trim(),
                    recipeLineParts[2].Trim(),
                    recipeLineParts[3].Trim(),
                    int.TryParse(recipeLineParts[4], out int prep) ? prep : 0,
                    int.TryParse(recipeLineParts[5], out int cook) ? cook : 0,
                    double.TryParse(recipeLineParts[6], out double cal) ? cal : 0,
                    int.TryParse(recipeLineParts[7], out int serv) ? serv : 0,
                    recipeLineParts[7].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    recipeLineParts[8].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).ToList(),
                    recipeLineParts[9].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    recipeLineParts[10].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).ToList()
                ));
            }
            catch
            {
                // Skip corrupted individual lines 
                continue;
            }
        }

        return databaseRecipes;
    }
}