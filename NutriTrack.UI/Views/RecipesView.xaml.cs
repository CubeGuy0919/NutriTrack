using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public partial class RecipesView : UserControl
{
    private readonly RecipeService _recipeService = new();
    private List<Recipe> _allRecipes = new();
    private ICollectionView? _view;

    /// <summary>"All" plus the real category names, backing the chip row.</summary>
    private readonly List<ToggleButton> _categoryChips = new();
    private string _selectedCategory = "All";

    public RecipesView()
    {
        InitializeComponent();
        LoadRecipes();
    }

    // ---------------------------------------------------------------
    // Loading
    // ---------------------------------------------------------------

    private void LoadRecipes()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string recipePath = Path.Combine(baseDir, "Recipes.txt");

        _allRecipes = File.Exists(recipePath)
            ? _recipeService.LoadRecipesFromTxt(recipePath)
            : new List<Recipe>();

        _view = CollectionViewSource.GetDefaultView(_allRecipes);
        _view.Filter = FilterRecipe;
        RecipeItemsControl.ItemsSource = _view;

        BuildCuisineOptions();
        BuildCategoryChips();
        ApplySort();
        RefreshResultCount();
    }

    private void BuildCuisineOptions()
    {
        CuisineCombo.Items.Clear();
        CuisineCombo.Items.Add(new ComboBoxItem { Content = "All cuisines", IsSelected = true });
        foreach (var cuisine in _allRecipes.Select(r => r.Cuisine).Distinct().OrderBy(c => c))
            CuisineCombo.Items.Add(new ComboBoxItem { Content = cuisine });
    }

    private void BuildCategoryChips()
    {
        CategoryChips.Children.Clear();
        _categoryChips.Clear();

        var categories = new[] { "All" }
            .Concat(_allRecipes.Select(r => r.Category).Distinct().OrderBy(c => c));

        foreach (var category in categories)
        {
            var chip = new ToggleButton
            {
                Content = category,
                Style = (Style)FindResource("ChipToggleStyle"),
                IsChecked = category == "All",
                Tag = category
            };
            chip.Click += CategoryChip_Click;
            _categoryChips.Add(chip);
            CategoryChips.Children.Add(chip);
        }
    }

    // ---------------------------------------------------------------
    // Filtering / sorting
    // ---------------------------------------------------------------

    private void CategoryChip_Click(object sender, RoutedEventArgs e)
    {
        var clicked = (ToggleButton)sender;
        _selectedCategory = (string)clicked.Tag;

        // Keep the chip row acting like a single-select group
        foreach (var chip in _categoryChips)
            chip.IsChecked = ReferenceEquals(chip, clicked);

        _view?.Refresh();
        RefreshResultCount();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view is null) return;
        ApplySort();
        _view.Refresh();
        RefreshResultCount();
    }

    private bool FilterRecipe(object obj)
    {
        if (obj is not Recipe recipe) return false;

        if (_selectedCategory != "All" && recipe.Category != _selectedCategory)
            return false;

        if (CuisineCombo.SelectedItem is ComboBoxItem cuisineItem &&
            cuisineItem.Content is string cuisine && cuisine != "All cuisines" &&
            recipe.Cuisine != cuisine)
            return false;

        if (TimeCombo.SelectedItem is ComboBoxItem timeItem && timeItem.Tag is string tagStr &&
            int.TryParse(tagStr, out int maxMinutes) && maxMinutes > 0)
        {
            int lowerBound = maxMinutes switch
            {
                60 => 30,
                90 => 60,
                9999 => 90,
                _ => 0
            };
            if (recipe.TotalTimeMinutes <= lowerBound || recipe.TotalTimeMinutes > maxMinutes)
                return false;
        }

        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            bool nameMatch = recipe.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool ingredientMatch = recipe.Ingredients.Any(i => i.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (!nameMatch && !ingredientMatch) return false;
        }

        return true;
    }

    private void ApplySort()
    {
        if (_view is null) return;
        _view.SortDescriptions.Clear();

        string sortChoice = (SortCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "";
        switch (sortChoice)
        {
            case "Sort: Quickest first":
                _view.SortDescriptions.Add(new SortDescription(nameof(Recipe.TotalTimeMinutes), ListSortDirection.Ascending));
                break;
            case "Sort: Lowest calories":
                _view.SortDescriptions.Add(new SortDescription(nameof(Recipe.CaloriesPerServing), ListSortDirection.Ascending));
                break;
            case "Sort: Highest calories":
                _view.SortDescriptions.Add(new SortDescription(nameof(Recipe.CaloriesPerServing), ListSortDirection.Descending));
                break;
            default:
                _view.SortDescriptions.Add(new SortDescription(nameof(Recipe.Name), ListSortDirection.Ascending));
                break;
        }
    }

    private void RefreshResultCount()
    {
        int count = _view?.Cast<object>().Count() ?? 0;
        ResultCountText.Text = count == 1 ? "1 recipe" : $"{count} recipes";
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        CuisineCombo.SelectedIndex = 0;
        TimeCombo.SelectedIndex = 0;
        SortCombo.SelectedIndex = 0;
        _selectedCategory = "All";
        foreach (var chip in _categoryChips)
            chip.IsChecked = (string)chip.Tag == "All";

        ApplySort();
        _view?.Refresh();
        RefreshResultCount();
    }

    // ---------------------------------------------------------------
    // Detail panel
    // ---------------------------------------------------------------

    private void RecipeCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Recipe recipe) return;
        ShowDetail(recipe);
    }

    private void ShowDetail(Recipe recipe)
    {
        DetailTitle.Text = recipe.Name;
        DetailMeta.Text = $"{recipe.Cuisine} · {recipe.Category} · {recipe.TimeLabel} · {recipe.CaloriesLabel}";
        DetailSensitivities.Text = recipe.SensitivitiesLabel;
        DetailIngredients.ItemsSource = recipe.Ingredients;
        DetailInstructions.ItemsSource = recipe.Instructions;

        DetailBorder.Visibility = Visibility.Visible;
        DetailColumn.Width = new GridLength(360);
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        DetailBorder.Visibility = Visibility.Collapsed;
        DetailColumn.Width = new GridLength(0);
    }
}
