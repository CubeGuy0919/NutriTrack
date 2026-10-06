using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public partial class RecipesView : UserControl
{
    private readonly RecipeImageService _imageService = new();
    private List<Recipe> _allRecipes = new();
    private List<Product> _products = new();

    private static string RecipesPath => AppPaths.RecipesPath;
    private static string ImagesDirectory => AppPaths.ImagesDirectory;
    private ICollectionView? _view;

    /// <summary>"All" plus the real category names, backing the chip row.</summary>
    private readonly List<ToggleButton> _categoryChips = new();
    private string _selectedCategory = "All";

    // ---- detail panel state ----
    private Recipe? _detailRecipe;
    private bool _suppressSliderEvents;
    private List<PantryItem> _pantryItems = new();
    private IReadOnlyList<IngredientAvailability> _availability = Array.Empty<IngredientAvailability>();

    // image slideshow
    private readonly List<BitmapImage> _slides = new();
    private int _slideIndex;
    private DispatcherTimer? _slideTimer;

    /// <param name="openRecipeId">When given, that recipe's detail panel opens straight away (used by the Dashboard and the Pantry).</param>
    public RecipesView(int? openRecipeId = null)
    {
        InitializeComponent();
        Unloaded += (_, _) => StopSlideshow();
        LoadRecipes();

        if (openRecipeId.HasValue)
        {
            var recipe = _allRecipes.FirstOrDefault(r => r.RecipeId == openRecipeId.Value);
            if (recipe != null) ShowDetail(recipe);
        }
    }

    // ---------------------------------------------------------------
    // Loading
    // ---------------------------------------------------------------

    private void LoadRecipes()
    {
        // The shared product list is the only source of accepted ingredients; recipes are read again from disk
        _products = AppServices.GetAllowedProducts();
        _allRecipes = AppServices.ReloadRecipes();

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
            bool ingredientMatch = recipe.Ingredients.Any(i => i.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase));
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
    // All scaling / nutrition / pantry maths lives in Core (IngredientScaler, RecipeNutritionCalculator,
    // RecipeAvailabilityCalculator, UnitConverter). This code only shows the results.

    private void RecipeCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Recipe recipe) return;
        ShowDetail(recipe);
    }

    private void ShowDetail(Recipe recipe)
    {
        _detailRecipe = recipe;

        DetailTitle.Text = recipe.Name;
        DetailMeta.Text = $"{recipe.Cuisine} · {recipe.Category}";
        DetailPrep.Text = $"{recipe.PrepTimeMinutes} min";
        DetailCook.Text = $"{recipe.CookTimeMinutes} min";
        DetailCalories.Text = recipe.CaloriesPerServing > 0 ? $"{recipe.CaloriesPerServing:0} kcal" : "–";

        DetailDescription.Text = recipe.Description;
        DetailDescription.Visibility = string.IsNullOrWhiteSpace(recipe.Description) ? Visibility.Collapsed : Visibility.Visible;

        ShowDetailImages(recipe);
        DetailSensitivities.Text = recipe.SensitivitiesLabel;
        DetailInstructions.ItemsSource = recipe.Instructions;

        LoadPantry();
        SelectUnitRadio();

        // The slider starts at the recipe's own (base) servings
        _suppressSliderEvents = true;
        ServingsSlider.Maximum = Math.Max(12, recipe.BaseServings);
        ServingsMaxText.Text = ((int)ServingsSlider.Maximum).ToString();
        ServingsSlider.Value = recipe.BaseServings;
        _suppressSliderEvents = false;

        BaseServingsNote.Text = recipe.HasKnownServings
            ? $"This recipe is written for {recipe.BaseServings} servings."
            : $"No servings value on file for this recipe - assuming {recipe.BaseServings}.";

        RefreshServings();

        DetailBorder.Visibility = Visibility.Visible;
        DetailColumn.Width = new GridLength(440);
        DetailScroll.ScrollToTop();
    }

    private void UnitSystem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && Enum.TryParse(tag, out UnitSystem system))
        {
            AppServices.DisplayUnits = system;
            RefreshServings();
        }
    }

    private void SelectUnitRadio()
    {
        UnitAsWrittenRadio.IsChecked = AppServices.DisplayUnits == UnitSystem.AsWritten;
        UnitMetricRadio.IsChecked = AppServices.DisplayUnits == UnitSystem.Metric;
        UnitImperialRadio.IsChecked = AppServices.DisplayUnits == UnitSystem.Imperial;
    }

    private void LoadPantry()
    {
        _pantryItems = AppServices.Pantry.GetAllPantryItems();
    }

    private void ServingsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Also fires while the window is still being built, before any recipe is open
        if (_suppressSliderEvents || _detailRecipe is null) return;
        RefreshServings();
    }

    /// <summary>Re-computes ingredients, pantry status and nutrition for the slider's current value. Runs on every slider move.</summary>
    private void RefreshServings()
    {
        if (_detailRecipe is null) return;

        int servings = (int)Math.Round(ServingsSlider.Value);
        SelectedServingsText.Text = $"Selected servings: {servings}";

        var system = AppServices.DisplayUnits;
        var scaled = IngredientScaler.Scale(_detailRecipe, servings, system);
        _availability = RecipeAvailabilityCalculator.Check(scaled, _pantryItems, system);
        DetailIngredients.ItemsSource = _availability;

        var nutrition = RecipeNutritionCalculator.Calculate(_detailRecipe, servings);
        NutritionRows.ItemsSource = nutrition.Lines;
        NutritionHeader.Visibility = nutrition.Lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NutritionTotalHeader.Text = servings == 1 ? "TOTAL (1 serving)" : $"TOTAL ({servings} servings)";

        if (nutrition.Lines.Count == 0)
        {
            NutritionNote.Text = "No nutrition values are on file for this recipe.";
            NutritionNote.Visibility = Visibility.Visible;
        }
        else if (!nutrition.HasMacros)
        {
            NutritionNote.Text = "Protein, carbohydrates and fat were not provided for this recipe.";
            NutritionNote.Visibility = Visibility.Visible;
        }
        else
        {
            NutritionNote.Visibility = Visibility.Collapsed;
        }

        AddMissingButton.IsEnabled = _availability.Any(a => a.NeedsPurchase);
        ShoppingMessage.Text = string.Empty;
    }

    private void AddMissing_Click(object sender, RoutedEventArgs e)
    {
        if (_detailRecipe is null) return;

        int servings = (int)Math.Round(ServingsSlider.Value);
        var toBuy = _availability.Where(a => a.NeedsPurchase).ToList();

        if (toBuy.Count == 0)
        {
            ShowShoppingMessage("Nothing to add - everything is already in your pantry.", isError: false);
            return;
        }

        try
        {
            var list = AppServices.Shopping.Load(AppPaths.ShoppingListPath);
            int changed = AppServices.Shopping.AddMissing(list, toBuy);
            AppServices.Shopping.Save(AppPaths.ShoppingListPath, list);

            ShowShoppingMessage(
                $"Added {changed} ingredient{(changed == 1 ? "" : "s")} for {servings} serving{(servings == 1 ? "" : "s")} to your shopping list.",
                isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowShoppingMessage($"The shopping list could not be saved: {ex.Message}", isError: true);
        }
    }

    private void ShowShoppingMessage(string text, bool isError)
    {
        ShoppingMessage.Foreground = isError ? Brushes.Firebrick : (Brush)FindResource("PrimaryBrush");
        ShoppingMessage.Text = text;
    }

    // ---- image slideshow ----

    /// <summary>
    /// Shows every existing image listed for the recipe (auto-advancing when there are several).
    /// With no usable image the default placeholder is shown together with "No image available".
    /// </summary>
    private void ShowDetailImages(Recipe recipe)
    {
        StopSlideshow();
        _slides.Clear();
        _slideIndex = 0;

        foreach (var name in recipe.ImageFileNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            string path = Path.Combine(ImagesDirectory, Path.GetFileName(name.Trim()));
            var bitmap = TryLoadBitmap(path);
            if (bitmap != null) _slides.Add(bitmap);
        }

        bool hasOwnImages = _slides.Count > 0;
        if (!hasOwnImages)
        {
            string? fallback = _imageService.ResolveImagePath(Array.Empty<string>(), ImagesDirectory);
            var bitmap = fallback == null ? null : TryLoadBitmap(fallback);
            if (bitmap != null) _slides.Add(bitmap);
        }

        NoImageText.Visibility = hasOwnImages ? Visibility.Collapsed : Visibility.Visible;

        bool multiple = hasOwnImages && _slides.Count > 1;
        var navVisibility = multiple ? Visibility.Visible : Visibility.Collapsed;
        SlidePrevButton.Visibility = navVisibility;
        SlideNextButton.Visibility = navVisibility;
        SlideCounterBorder.Visibility = navVisibility;

        ShowSlide(0);
        if (multiple) StartSlideshow();
    }

    private static BitmapImage? TryLoadBitmap(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // do not keep the file locked
            bitmap.DecodePixelWidth = 640;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null; // unreadable / corrupt image: treat it as missing instead of crashing
        }
    }

    private void ShowSlide(int index)
    {
        if (_slides.Count == 0)
        {
            DetailImage.Source = null;
            return;
        }

        _slideIndex = ((index % _slides.Count) + _slides.Count) % _slides.Count;
        DetailImage.Source = _slides[_slideIndex];
        SlideCounterText.Text = $"{_slideIndex + 1} / {_slides.Count}";
    }

    private void StartSlideshow()
    {
        if (_slideTimer is null)
        {
            _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _slideTimer.Tick += (_, _) => ShowSlide(_slideIndex + 1);
        }

        _slideTimer.Stop();
        _slideTimer.Start();
    }

    private void StopSlideshow() => _slideTimer?.Stop();

    private void PrevSlide_Click(object sender, RoutedEventArgs e)
    {
        ShowSlide(_slideIndex - 1);
        StartSlideshow(); // restart the 5 s countdown after a manual change
    }

    private void NextSlide_Click(object sender, RoutedEventArgs e)
    {
        ShowSlide(_slideIndex + 1);
        StartSlideshow();
    }

    // ---------------------------------------------------------------
    // Add recipe
    // ---------------------------------------------------------------

    private void AddRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_products.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "The product list (Products.txt) could not be loaded, so ingredients cannot be validated.\n\nA recipe cannot be added right now.",
                "Add Recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new AddRecipeWindow(AppServices.Recipes, _products, _allRecipes, RecipesPath, ImagesDirectory)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.CreatedRecipe != null)
        {
            // Reload from disk so the list shows exactly what was persisted
            CloseDetail_Click(this, new RoutedEventArgs());
            LoadRecipes();
            ClearFilters_Click(this, new RoutedEventArgs()); // make sure the new recipe is visible
        }
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        StopSlideshow();
        _detailRecipe = null;
        DetailBorder.Visibility = Visibility.Collapsed;
        DetailColumn.Width = new GridLength(0);
    }
}
