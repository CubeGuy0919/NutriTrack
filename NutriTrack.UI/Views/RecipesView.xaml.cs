using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public partial class RecipesView : UserControl
{
    private readonly RecipeService _recipeService = new();

    public RecipesView()
    {
        InitializeComponent();
        LoadRecipes();
    }

    private void LoadRecipes()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string recipePath = Path.Combine(baseDir, "Recipes.txt");

        if (File.Exists(recipePath))
        {
            var recipes = _recipeService.LoadRecipesFromTxt(recipePath);
            RecipesGrid.ItemsSource = recipes;
        }
        else
        {
            MessageBox.Show("Could not find Recipes.txt file.");
        }
    }
}