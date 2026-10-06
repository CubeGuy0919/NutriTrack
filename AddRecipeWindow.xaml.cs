using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

/// <summary>
/// "Add New Recipe" dialog. UI only: it reads the form, builds a Recipe and hands it to RecipeService,
/// which does the validation (products, duplicate names) and the saving.
/// </summary>
public partial class AddRecipeWindow : Window
{
    private readonly RecipeService _recipeService;
    private readonly IReadOnlyList<Product> _products;
    private readonly List<Recipe> _existingRecipes;
    private readonly ProductMatcher _matcher;
    private readonly string _recipesFilePath;
    private readonly string _imagesDirectory;

    private readonly ObservableCollection<IngredientItem> _ingredients = new();
    private int _editIndex = -1;
    private string? _selectedImagePath;

    /// <summary>The recipe that was saved (null if the dialog was cancelled).</summary>
    public Recipe? CreatedRecipe { get; private set; }

    public AddRecipeWindow(
        RecipeService recipeService,
        IReadOnlyList<Product> products,
        List<Recipe> existingRecipes,
        string recipesFilePath,
        string imagesDirectory)
    {
        InitializeComponent();

        _recipeService = recipeService;
        _products = products;
        _existingRecipes = existingRecipes;
        _recipesFilePath = recipesFilePath;
        _imagesDirectory = imagesDirectory;
        _matcher = new ProductMatcher(products);

        CategoryCombo.ItemsSource = existingRecipes.Select(r => r.Category).Where(c => c.Length > 0).Distinct().OrderBy(c => c).ToList();
        CuisineCombo.ItemsSource = existingRecipes.Select(r => r.Cuisine).Where(c => c.Length > 0).Distinct().OrderBy(c => c).ToList();

        IngredientCombo.ItemsSource = products.Select(p => p.Name).OrderBy(n => n).ToList();
        UnitCombo.ItemsSource = IngredientParser.CommonUnits;

        IngredientList.ItemsSource = _ingredients;
        _ingredients.CollectionChanged += (_, _) =>
            IngredientsEmptyText.Visibility = _ingredients.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        NameBox.Focus();
    }

    // ---------------------------------------------------------------
    // Ingredients
    // ---------------------------------------------------------------

    private void IngredientCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Picking a product pre-fills its natural unit (g / ml) if the unit box is still empty
        if (IngredientCombo.SelectedItem is string name && string.IsNullOrWhiteSpace(UnitCombo.Text))
        {
            var product = _matcher.Match(name);
            if (product != null) UnitCombo.Text = product.Unit;
        }
    }

    private void AddIngredient_Click(object sender, RoutedEventArgs e)
    {
        string typedName = IngredientCombo.Text?.Trim() ?? string.Empty;
        if (typedName.Length == 0)
        {
            ShowIngredientStatus("Choose an ingredient from the list (or type its name).");
            return;
        }

        if (!IngredientParser.TryParseQuantity(QuantityBox.Text, out double quantity))
        {
            ShowIngredientStatus("Enter a quantity greater than 0, for example 500, 1.5 or 1/2.");
            return;
        }

        var product = _matcher.Match(typedName);
        var item = new IngredientItem
        {
            Name = product?.Name ?? typedName,
            Quantity = quantity,
            Unit = IngredientParser.NormalizeUnit(UnitCombo.Text),
            ProductId = product?.ProductId
        };

        if (_editIndex >= 0 && _editIndex < _ingredients.Count)
            _ingredients[_editIndex] = item;
        else
            _ingredients.Add(item);

        if (product == null)
        {
            var hint = _matcher.Suggest(typedName).Select(p => p.Name).ToList();
            ShowIngredientStatus(
                $"\"{typedName}\" is not in the product list. It was added but marked, and the recipe cannot be saved until it is replaced" +
                (hint.Count > 0 ? $" (did you mean: {string.Join(", ", hint)}?)." : "."));
        }
        else
        {
            HideIngredientStatus();
        }

        ResetIngredientEditor();
    }

    private void EditIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IngredientItem item }) return;

        int index = _ingredients.IndexOf(item);
        if (index < 0) return;

        _editIndex = index;
        IngredientCombo.Text = item.Name;
        QuantityBox.Text = IngredientItem.FormatQuantity(item.Quantity);
        UnitCombo.Text = item.Unit;

        AddIngredientButton.Content = "Update Ingredient";
        CancelEditButton.Visibility = Visibility.Visible;
        HideIngredientStatus();
    }

    private void RemoveIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IngredientItem item }) return;

        int index = _ingredients.IndexOf(item);
        if (index < 0) return;

        _ingredients.RemoveAt(index);

        if (_editIndex == index) ResetIngredientEditor();
        else if (_editIndex > index) _editIndex--;
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        ResetIngredientEditor();
        HideIngredientStatus();
    }

    private void ResetIngredientEditor()
    {
        _editIndex = -1;
        IngredientCombo.SelectedIndex = -1;
        IngredientCombo.Text = string.Empty;
        QuantityBox.Text = string.Empty;
        UnitCombo.SelectedIndex = -1;
        UnitCombo.Text = string.Empty;
        AddIngredientButton.Content = "Add Ingredient";
        CancelEditButton.Visibility = Visibility.Collapsed;
    }

    private void ShowIngredientStatus(string message)
    {
        IngredientStatusText.Text = message;
        IngredientStatusText.Visibility = Visibility.Visible;
    }

    private void HideIngredientStatus() => IngredientStatusText.Visibility = Visibility.Collapsed;

    // ---------------------------------------------------------------
    // Image
    // ---------------------------------------------------------------

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a recipe image",
            Filter = RecipeImageService.FileDialogFilter
        };

        if (dialog.ShowDialog(this) != true) return;

        _selectedImagePath = dialog.FileName;
        ImageNameText.Text = Path.GetFileName(_selectedImagePath);
        ClearImageButton.Visibility = Visibility.Visible;
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        _selectedImagePath = null;
        ImageNameText.Text = "No image - the default placeholder will be used.";
        ClearImageButton.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------
    // Save
    // ---------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // A half-finished ingredient row must not be silently dropped
        if (!string.IsNullOrWhiteSpace(IngredientCombo.Text) || !string.IsNullOrWhiteSpace(QuantityBox.Text))
        {
            MessageBox.Show(this,
                "You have an ingredient in the editor that has not been added.\n\nClick \"Add Ingredient\" (or clear the fields) first.",
                "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var problems = new List<string>();
        var recipe = BuildRecipeFromForm(problems);

        if (problems.Count > 0 || recipe == null)
        {
            MessageBox.Show(this,
                "Cannot save recipe." + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "• " + p)),
                "Cannot save recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = _recipeService.AddRecipe(
            _recipesFilePath, recipe, _existingRecipes, _products, _selectedImagePath, _imagesDirectory);

        if (result.SavedRecipe == null)
        {
            MessageBox.Show(this, result.ToUserMessage(), "Recipe rejected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            if (result.DuplicateName) NameBox.Focus();
            return;
        }

        CreatedRecipe = result.SavedRecipe;
        MessageBox.Show(this, "Recipe saved successfully.", "NutriTrack",
            MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Turns the text boxes into a Recipe. Only parsing problems are reported here; business rules live in RecipeService.</summary>
    private Recipe? BuildRecipeFromForm(List<string> problems)
    {
        int prep = ReadInt(PrepBox.Text, "Preparation time", problems, min: 0);
        int cook = ReadInt(CookBox.Text, "Cooking time", problems, min: 0);
        int servings = ReadInt(ServingsBox.Text, "Base servings", problems, min: 1);

        double calories = 0;
        if (!TryReadDouble(CaloriesBox.Text, out calories) || calories < 0)
            problems.Add("Calories per serving must be a number (0 or more).");

        double? protein = ReadOptionalDouble(ProteinBox.Text, "Protein", problems);
        double? carbs = ReadOptionalDouble(CarbsBox.Text, "Carbohydrates", problems);
        double? fat = ReadOptionalDouble(FatBox.Text, "Fat", problems);

        if (problems.Count > 0) return null;

        var instructions = new List<string>();
        int step = 1;
        foreach (var raw in InstructionsBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            instructions.Add(line.StartsWith("Step ", StringComparison.OrdinalIgnoreCase) ? line : $"Step {step} {line}");
            step++;
        }

        return new Recipe
        {
            Name = NameBox.Text.Trim(),
            Category = CategoryCombo.Text.Trim(),
            Cuisine = CuisineCombo.Text.Trim(),
            Description = DescriptionBox.Text.Trim(),
            PrepTimeMinutes = prep,
            CookTimeMinutes = cook,
            Servings = servings,
            CaloriesPerServing = calories,
            ProteinPerServing = protein,
            CarbsPerServing = carbs,
            FatPerServing = fat,
            FoodSensitivities = SensitivitiesBox.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
            Ingredients = _ingredients.Select(i => i.Clone()).ToList(),
            Instructions = instructions
        };
    }

    private static int ReadInt(string text, string label, List<string> problems, int min)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min)
            return value;

        problems.Add($"{label} must be a whole number of {min} or more.");
        return 0;
    }

    private static bool TryReadDouble(string? text, out double value) =>
        double.TryParse((text ?? string.Empty).Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double? ReadOptionalDouble(string? text, string label, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(text)) return null; // left empty = not provided, never invented

        if (TryReadDouble(text, out double value) && value >= 0) return value;

        problems.Add($"{label} must be a number (0 or more) or left empty.");
        return null;
    }
}
