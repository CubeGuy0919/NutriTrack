using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;

namespace NutriTrack.UI.Views;

public partial class DashboardView : UserControl
{
    private Recipe? _recipeOfTheDay;

    public DashboardView()
    {
        InitializeComponent();
        LoadDashboardData();
    }

    public void LoadDashboardData()
    {
        var recipes = AppServices.GetLoadedRecipes();

        // 1. Receipts Metrics
        int receiptsThisMonth = AppServices.Receipts.GetReceiptsCountThisMonth();
        decimal spendingThisMonth = AppServices.Receipts.GetTotalSpendingThisMonth();
        ReceiptsCountText.Text = receiptsThisMonth.ToString();
        ReceiptsSpendingText.Text = $"{spendingThisMonth:N0} Ft total spent";

        // 2. Recipes Metrics
        RecipesCountText.Text = recipes.Count.ToString();
        var cuisinesCount = recipes.Select(r => r.Cuisine).Distinct().Count();
        RecipesCuisinesText.Text = $"{cuisinesCount} cuisines available";

        // 3. Average Calories Metric
        if (recipes.Count > 0)
        {
            double avgCal = recipes.Average(r => r.CaloriesPerServing);
            AvgCaloriesText.Text = $"{avgCal:0} kcal";
            AvgCaloriesSubtext.Text = "Average across database";
        }
        else
        {
            AvgCaloriesText.Text = "—";
            AvgCaloriesSubtext.Text = "No recipes loaded";
        }

        // 4. Recipe of the Day
        SetupRecipeOfTheDay(recipes);

        // 5. Use Soon Section
        SetupUseSoonSection(recipes);
    }

    private void SetupRecipeOfTheDay(List<Recipe> recipes)
    {
        if (recipes.Count == 0)
        {
            RecipeNameText.Text = "No recipes found";
            RecipeCategoryText.Text = "Add recipes to database";
            RecipeTimeText.Text = "0 min";
            RecipeCaloriesText.Text = "0 kcal";
            RecipeIngredientsCountText.Text = "0 items";
            RecipePantryMatchBadge.Visibility = Visibility.Collapsed;
            return;
        }

        // Prefer recipe with best match from pantry
        var availableRecipes = AppServices.Pantry.GetAvailableRecipes(recipes);
        var topPantryMatch = availableRecipes.FirstOrDefault();

        if (topPantryMatch != null)
        {
            _recipeOfTheDay = topPantryMatch.Recipe;
            RecipePantryMatchBadge.Visibility = Visibility.Visible;
            RecipePantryMatchText.Text = topPantryMatch.MatchBadge;
        }
        else
        {
            // Deterministic fall-back based on day of year
            int dayIndex = DateTime.Today.DayOfYear % recipes.Count;
            _recipeOfTheDay = recipes[dayIndex];
            RecipePantryMatchBadge.Visibility = Visibility.Collapsed;
        }

        RecipeNameText.Text = _recipeOfTheDay.Name;
        RecipeCategoryText.Text = $"{_recipeOfTheDay.Cuisine} · {_recipeOfTheDay.Category}";
        RecipeTimeText.Text = _recipeOfTheDay.TimeLabel;
        RecipeCaloriesText.Text = _recipeOfTheDay.CaloriesLabel;
        RecipeIngredientsCountText.Text = $"{_recipeOfTheDay.Ingredients.Count} items";
    }

    private void SetupUseSoonSection(List<Recipe> recipes)
    {
        var expiringItems = AppServices.Pantry.GetExpiringSoonItems(3).Take(4).ToList();
        var useSoonList = new List<UseSoonViewModel>();

        foreach (var item in expiringItems)
        {
            var suggestedRecipe = AppServices.Pantry.FindSuggestedRecipeForPantryItem(item, recipes);
            useSoonList.Add(new UseSoonViewModel
            {
                Item = item,
                Recipe = suggestedRecipe
            });
        }

        DashboardUseSoonList.ItemsSource = useSoonList;
        UseSoonCountText.Text = $"{useSoonList.Count} items";
        NoExpiringItemsText.Visibility = useSoonList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenRecipeOfTheDay_Click(object sender, RoutedEventArgs e)
    {
        if (_recipeOfTheDay != null)
        {
            var detailWindow = new RecipeDetailWindow(_recipeOfTheDay)
            {
                Owner = Window.GetWindow(this)
            };
            detailWindow.ShowDialog();
        }
    }

    private void ViewSuggestedRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Recipe recipe)
        {
            var detailWindow = new RecipeDetailWindow(recipe)
            {
                Owner = Window.GetWindow(this)
            };
            detailWindow.ShowDialog();
        }
    }
}