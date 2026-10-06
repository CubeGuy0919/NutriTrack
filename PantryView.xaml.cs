using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public class UseSoonViewModel
{
    private string? _recipeNameDisplay;
    public PantryItem Item { get; set; } = new();
    public Recipe? Recipe { get; set; }
    public string RecipeNameDisplay
    {
        get => _recipeNameDisplay ?? Recipe?.Name ?? "No matching recipe found";
        set => _recipeNameDisplay = value;
    }
    public Visibility HasRecipeVisibility => Recipe != null ? Visibility.Visible : Visibility.Collapsed;
}

public partial class PantryView : UserControl
{
    private List<PantryItem> _allPantryItems = new();
    private PantryItem? _editingItem;

    public PantryView()
    {
        InitializeComponent();
        PopulateProductSelector();
        LoadPantry();
    }

    private void PopulateProductSelector()
    {
        var products = AppServices.GetAllowedProducts();
        DialogProductCombo.Items.Clear();
        foreach (var p in products)
        {
            DialogProductCombo.Items.Add(p.Name);
        }
    }

    public void LoadPantry()
    {
        _allPantryItems = AppServices.Pantry.GetAllPantryItems();
        UpdateCategoryFilterOptions();
        ApplyFilter();
        UpdateIntegrations();
    }

    private void UpdateCategoryFilterOptions()
    {
        string currentCategory = (CategoryFilterCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "All categories";

        CategoryFilterCombo.Items.Clear();
        CategoryFilterCombo.Items.Add(new ComboBoxItem { Content = "All categories", IsSelected = true });

        var categories = _allPantryItems
            .Select(p => p.Category?.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c);

        foreach (var cat in categories)
        {
            var item = new ComboBoxItem { Content = cat };
            if (cat!.Equals(currentCategory, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
            }
            CategoryFilterCombo.Items.Add(item);
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        CategoryFilterCombo.SelectedIndex = 0;
        ExpiryFilterCombo.SelectedIndex = 0;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (PantryItemsControl is null) return;

        var query = _allPantryItems.AsEnumerable();

        // 1. Text Search
        var searchText = SearchBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(p => p.ProductName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Category Filter
        if (CategoryFilterCombo?.SelectedItem is ComboBoxItem catItem &&
            catItem.Content is string category && category != "All categories")
        {
            query = query.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Expiry Filter
        if (ExpiryFilterCombo?.SelectedItem is ComboBoxItem expItem &&
            expItem.Content is string expChoice)
        {
            if (expChoice.StartsWith("Expiring soon", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ExpiryStatus == ExpiryStatus.ExpiringSoon);
            }
            else if (expChoice == "Expired")
            {
                query = query.Where(p => p.ExpiryStatus == ExpiryStatus.Expired);
            }
            else if (expChoice.StartsWith("Fresh", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ExpiryStatus == ExpiryStatus.Normal);
            }
            else if (expChoice == "No expiry date")
            {
                query = query.Where(p => p.ExpiryStatus == ExpiryStatus.NoDate);
            }
        }

        var filtered = query.ToList();
        PantryItemsControl.ItemsSource = filtered;

        int totalCount = filtered.Count;
        PantrySummaryText.Text = totalCount == 1 ? "1 item in pantry" : $"{totalCount} items in pantry";

        int expiringCount = _allPantryItems.Count(p => p.ExpiryStatus == ExpiryStatus.ExpiringSoon || p.ExpiryStatus == ExpiryStatus.Expired);
        ExpiringCountBadge.Text = expiringCount > 0 ? $"⚠️ {expiringCount} items need attention" : "✓ All items fresh";
        ExpiringCountBadge.Foreground = expiringCount > 0
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50));

        EmptyState.Visibility = totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateIntegrations()
    {
        var allRecipes = AppServices.GetLoadedRecipes();

        // 1. "Use soon" items (within 3 days or expired)
        var expiringItems = AppServices.Pantry.GetExpiringSoonItems(3);
        var useSoonList = new List<UseSoonViewModel>();

        foreach (var item in expiringItems)
        {
            var suggestedRecipe = AppServices.Pantry.FindSuggestedRecipeForPantryItem(item, allRecipes);
            useSoonList.Add(new UseSoonViewModel
            {
                Item = item,
                Recipe = suggestedRecipe
            });
        }

        UseSoonItemsControl.ItemsSource = useSoonList;
        UseSoonCountText.Text = $"{useSoonList.Count} items";
        NoExpiringItemsText.Visibility = useSoonList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 2. "What can I cook?" recipes
        var matchedRecipes = AppServices.Pantry.GetAvailableRecipes(allRecipes)
            .Take(12)
            .ToList();

        AvailableRecipesControl.ItemsSource = matchedRecipes;
        NoAvailableRecipesText.Visibility = matchedRecipes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ViewSuggestedRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Recipe recipe)
            AppServices.OpenRecipe(recipe);
    }

    // -----------------------------------------------------------------
    // Add / Edit Modal Dialog
    // -----------------------------------------------------------------

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        OpenItemDialog(null);
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PantryItem item)
        {
            OpenItemDialog(item);
        }
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PantryItem item)
        {
            var result = MessageBox.Show(
                $"Remove '{item.ProductName}' from your pantry?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AppServices.Pantry.DeletePantryItem(item.PantryItemId);
                LoadPantry();
                ReportSaveProblem();
            }
        }
    }

    private void OpenItemDialog(PantryItem? itemToEdit)
    {
        _editingItem = itemToEdit;

        if (itemToEdit != null)
        {
            ModalTitleText.Text = "Edit Pantry Item";
            DialogProductCombo.Text = itemToEdit.ProductName;
            DialogCategoryInput.Text = itemToEdit.Category ?? string.Empty;
            DialogQuantityInput.Text = itemToEdit.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
            DialogUnitInput.Text = itemToEdit.Unit;
            DialogExpiryPicker.SelectedDate = itemToEdit.ExpiryDate;
        }
        else
        {
            ModalTitleText.Text = "Add Pantry Item";
            DialogProductCombo.Text = string.Empty;
            DialogCategoryInput.Text = string.Empty;
            DialogQuantityInput.Text = "1";
            DialogUnitInput.Text = "pcs";
            DialogExpiryPicker.SelectedDate = DateTime.Today.AddDays(7);
        }

        ItemModal.Visibility = Visibility.Visible;
    }

    private void DialogProductCombo_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (DialogProductCombo.SelectedItem is string name)
        {
            DialogProductCombo.Text = name;
            var catalogProduct = AppServices.GetAllowedProducts()
                .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (catalogProduct != null)
            {
                if (!string.IsNullOrEmpty(catalogProduct.Category))
                    DialogCategoryInput.Text = catalogProduct.Category;
                if (!string.IsNullOrEmpty(catalogProduct.Unit))
                    DialogUnitInput.Text = catalogProduct.Unit;
            }
        }
    }

    private void SaveItemDialog_Click(object sender, RoutedEventArgs e)
    {
        string name = DialogProductCombo.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter or select a product name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!IngredientParser.TryParseQuantity(DialogQuantityInput.Text, out double qty))
        {
            MessageBox.Show("Please enter a valid positive quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string category = DialogCategoryInput.Text?.Trim() ?? "General";
        string unit = DialogUnitInput.Text?.Trim() ?? "pcs";
        DateTime? expiryDate = DialogExpiryPicker.SelectedDate;

        // Typed names such as "tomatoes" or "greek yogurt" still link to the catalog product
        var catalogProduct = AppServices.GetProductMatcher().Match(name);

        int productId = catalogProduct?.ProductId ?? 0;
        if (catalogProduct != null) name = catalogProduct.Name;

        if (_editingItem != null)
        {
            _editingItem.ProductId = productId;
            _editingItem.ProductName = name;
            _editingItem.Category = category;
            _editingItem.Quantity = qty;
            _editingItem.Unit = unit;
            _editingItem.ExpiryDate = expiryDate;
            AppServices.Pantry.UpdatePantryItem(_editingItem);
        }
        else
        {
            AppServices.Pantry.AddOrIncrease(productId, name, category, qty, unit, expiryDate);
        }

        ItemModal.Visibility = Visibility.Collapsed;
        LoadPantry();
        ReportSaveProblem();
    }

    private static void ReportSaveProblem()
    {
        if (AppServices.Pantry.LastSaveError is { } error)
            MessageBox.Show($"The pantry could not be saved:\n{error}", "Pantry", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void CancelItemDialog_Click(object sender, RoutedEventArgs e)
    {
        ItemModal.Visibility = Visibility.Collapsed;
    }
}
