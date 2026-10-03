using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NutriTrack.Core.Models;

namespace NutriTrack.UI.Views;

public partial class RecipeDetailWindow : Window
{
    private Recipe _recipe;
    private readonly List<BitmapImage> _loadedBitmaps = new();
    private int _currentImageIndex = 0;
    private DispatcherTimer? _slideshowTimer;

    public RecipeDetailWindow(Recipe recipe)
    {
        InitializeComponent();
        _recipe = recipe;
        PopulateDetails(recipe);
        Setup3ImageSlideshow(recipe.ImageFileNames);

        // Initialize slider maximum to BaseServings or 12
        ServingsSlider.Value = _recipe.Servings > 0 ? _recipe.Servings : 1;
    }

    private void Setup3ImageSlideshow(List<string> imageNames)
    {
        if (imageNames == null || imageNames.Count == 0) return;

        string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");

        foreach (string name in imageNames)
        {
            string fullPath = Path.Combine(baseDir, name);
            if (File.Exists(fullPath))
            {
                _loadedBitmaps.Add(LoadBitmap(fullPath));
            }
        }

        if (_loadedBitmaps.Count == 0) return;

        RecipeImage1.Source = _loadedBitmaps[0];

        // Start 5-second rotation if multiple images (up to 3) are loaded
        if (_loadedBitmaps.Count > 1)
        {
            _slideshowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _slideshowTimer.Tick += SlideshowTimer_Tick;
            _slideshowTimer.Start();
        }
    }

    private void SlideshowTimer_Tick(object? sender, EventArgs e)
    {
        // Cycle index across available images (0 -> 1 -> 2 -> 0)
        int nextIndex = (_currentImageIndex + 1) % _loadedBitmaps.Count;

        // Load next bitmap into overlay control
        RecipeImage2.Source = _loadedBitmaps[nextIndex];

        // Create 1-second fade-in animation
        var fadeInAnimation = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromSeconds(1)
        };

        fadeInAnimation.Completed += (s, a) =>
        {
            // Swap bitmap sources once animation finishes
            RecipeImage1.Source = _loadedBitmaps[nextIndex];
            RecipeImage2.Opacity = 0; // Reset overlay
            _currentImageIndex = nextIndex;
        };

        // Start cross-fade transition
        RecipeImage2.BeginAnimation(OpacityProperty, fadeInAnimation);
    }

    private BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Prevents file locking on disk
        bitmap.EndInit();
        return bitmap;
    }

    private void PopulateDetails(Recipe recipe)
    {
        if (recipe == null) return;

        // Map text properties to XAML controls
        RecipeNameText.Text = recipe.Name;
        CuisineCategoryText.Text = $"{recipe.Cuisine} • {recipe.Category}";
        PrepTimeText.Text = $"{recipe.PrepTimeMinutes} min";
        CookTimeText.Text = $"{recipe.CookTimeMinutes} min";
        CaloriesText.Text = $"{recipe.CaloriesPerServing:F0} kcal";

        // Format complex text fields
        SensitivitiesText.Text = string.IsNullOrEmpty(recipe.FormattedSensitivities)
            ? "None specified"
            : recipe.FormattedSensitivities;

        IngredientsText.Text = recipe.FormattedIngredients;
        InstructionsText.Text = recipe.FormattedInstructions;
    }

    private void ServingsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_recipe == null) return;

        int newServings = (int)e.NewValue;
        if (ServingsValueText != null)
        {
            ServingsValueText.Text = newServings.ToString();
        }

        UpdateIngredientsAndCalories();
    }

    private void UnitSystem_Changed(object sender, RoutedEventArgs e)
    {
        UpdateIngredientsAndCalories();
    }

    private void UpdateIngredientsAndCalories()
    {
        if (_recipe == null) return;

        int currentServings = (int)ServingsSlider.Value;
        int baseServings = _recipe.Servings > 0 ? _recipe.Servings : 1;

        // Calculate scaling multiplier
        double scaleFactor = (double)currentServings / baseServings;
        bool useImperial = ImperialRadio?.IsChecked == true;

        // Recalculate total calories displayed
        CaloriesText.Text = $"{_recipe.CaloriesPerServing * scaleFactor:F0} kcal";

        // Re-render ingredients using the scaling method
        IngredientsText.Text = _recipe.GetScaledAndConvertedIngredients(scaleFactor, useImperial);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Stop timer to prevent memory leaks when window closes
        _slideshowTimer?.Stop();
        Close();
    }
}