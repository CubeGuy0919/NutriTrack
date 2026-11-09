using System.IO;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

/// <summary>Shows ShoppingList.txt. Items are added from the recipe detail panel; here they can be removed.</summary>
public partial class ShoppingListView : UserControl
{
    private readonly ShoppingListService _service = AppServices.Shopping;
    private List<ShoppingListItem> _items = new();

    public ShoppingListView()
    {
        InitializeComponent();

        try
        {
            bool firstRun = !File.Exists(AppPaths.ShoppingListPath);
            _items = _service.Load(AppPaths.ShoppingListPath);

            // First-run sample list makes the "Bought -> Pantry" workflow easy to demonstrate.
            if (firstRun)
            {
                _items = new List<ShoppingListItem>
                {
                    new() { ProductId = 15, Name = "Broccoli", Quantity = 500, Unit = "g" },
                    new() { ProductId = 5, Name = "Greek Yogurt (Plain)", Quantity = 400, Unit = "g" },
                    new() { ProductId = 10, Name = "Whole Wheat Bread", Quantity = 500, Unit = "g" }
                };
                _service.Save(AppPaths.ShoppingListPath, _items);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _items = new List<ShoppingListItem>();
            StatusText.Text = $"ShoppingList.txt could not be read or saved: {ex.Message}";
        }

        RefreshList();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShoppingListItem item }) return;

        _items.Remove(item);
        if (Save()) RefreshList();
    }

    /// <summary>Moves a bought item into the pantry (when it is a known product) and takes it off the list.</summary>
    private void Bought_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShoppingListItem item }) return;

        if (item.ProductId.HasValue)
        {
            var product = AppServices.GetAllowedProducts().FirstOrDefault(p => p.ProductId == item.ProductId.Value);

            AppServices.Pantry.AddOrIncrease(item.ProductId.Value, product?.Name ?? item.Name, product?.Category,
                item.Quantity, item.Unit, expiryDate: null);

            if (AppServices.Pantry.LastSaveError is { } error)
            {
                StatusText.Text = $"The pantry could not be saved: {error}";
                return;
            }
        }

        _items.Remove(item);
        if (Save()) RefreshList();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;

        var answer = MessageBox.Show(Window.GetWindow(this), "Remove everything from the shopping list?",
            "Shopping List", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _items.Clear();
        if (Save()) RefreshList();
    }

    private bool Save()
    {
        try
        {
            _service.Save(AppPaths.ShoppingListPath, _items);
            StatusText.Text = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"The shopping list could not be saved: {ex.Message}";
            return false;
        }
    }

    private void RefreshList()
    {
        ShoppingList.ItemsSource = null;
        ShoppingList.ItemsSource = _items.ToList();
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CountText.Text = _items.Count == 1 ? "1 item" : $"{_items.Count} items";
    }
}
