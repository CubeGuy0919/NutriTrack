using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public partial class RecipesView : UserControl
{
    private readonly RecipeService _recipeService = new();
    private List<Recipe> _allRecipes = new();

    public RecipesView()
    {
        InitializeComponent();
        LoadDataAndInitializeFilters();
    }

    private void LoadDataAndInitializeFilters()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string recipePath = Path.Combine(baseDir, "Recipes.txt");

        if (File.Exists(recipePath))
        {
            _allRecipes = _recipeService.LoadRecipesFromTxt(recipePath);

            // Populate Category Dropdown
            var categories = _allRecipes.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
            categories.Insert(0, "All Categories");
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0;

            // Populate Cuisine Dropdown
            var cuisines = _allRecipes.Select(r => r.Cuisine).Distinct().OrderBy(c => c).ToList();
            cuisines.Insert(0, "All Cuisines");
            CuisineComboBox.ItemsSource = cuisines;
            CuisineComboBox.SelectedIndex = 0;

            ApplyFilters();
        }
        else
        {
            MessageBox.Show("Could not find Recipes.txt file.", "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilters()
    {
        if (_allRecipes == null) return;

        string searchText = SearchTextBox.Text?.Trim().ToLower() ?? string.Empty;
        string selectedCategory = CategoryComboBox.SelectedItem as string;
        string selectedCuisine = CuisineComboBox.SelectedItem as string;

        var filtered = _allRecipes.Where(r =>
        {
            // Search text matching Name or Ingredients
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 r.Name.ToLower().Contains(searchText) ||
                                 r.Ingredients.Any(i => i.ToLower().Contains(searchText));

            // Category matching
            bool matchesCategory = selectedCategory == null ||
                                    selectedCategory == "All Categories" ||
                                    r.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase);

            // Cuisine matching
            bool matchesCuisine = selectedCuisine == null ||
                                  selectedCuisine == "All Cuisines" ||
                                  r.Cuisine.Equals(selectedCuisine, StringComparison.OrdinalIgnoreCase);

            return matchesSearch && matchesCategory && matchesCuisine;
        }).ToList();

        RecipeDataGrid.ItemsSource = filtered;
        ResultCountText.Text = $"Showing {filtered.Count} of {_allRecipes.Count} recipes";
    }

    private void FilterInput_Changed(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        CategoryComboBox.SelectedIndex = 0;
        CuisineComboBox.SelectedIndex = 0;
        ApplyFilters();
    }

    private void RecipeDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (RecipeDataGrid.SelectedItem is Recipe selectedRecipe)
        {
            RecipeDetailWindow detailWindow = new RecipeDetailWindow(selectedRecipe)
            {
                Owner = Window.GetWindow(this)
            };
            detailWindow.ShowDialog(); 
        }
    }
}