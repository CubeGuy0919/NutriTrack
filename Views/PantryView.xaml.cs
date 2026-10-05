using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

/// <summary>Simple pantry editor: add stock of a product, remove entries. Saved to Pantry.txt immediately.</summary>
public partial class PantryView : UserControl
{
    private readonly PantryService _pantryService = new();
    private readonly ProductService _productService = new();
    private List<Product> _products = new();
    private List<PantryItem> _items = new();

    public PantryView()
    {
        InitializeComponent();

        try
        {
            _products = File.Exists(AppPaths.ProductsPath)
                ? _productService.LoadAllowedProducts(AppPaths.ProductsPath).OrderBy(p => p.Name).ToList()
                : new List<Product>();
        }
        catch
        {
            _products = new List<Product>();
        }

        ProductCombo.ItemsSource = _products;
        UnitCombo.ItemsSource = new[] { "g", "kg", "ml", "l", "pcs" };
        UnitCombo.SelectedIndex = 0;

        try
        {
            _items = _pantryService.Load(AppPaths.PantryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _items = new List<PantryItem>();
            ShowStatus($"Pantry.txt could not be read: {ex.Message}", isError: true);
        }

        RefreshList();
    }

    private void ProductCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Pre-select the product's natural unit (g / ml)
        if (ProductCombo.SelectedItem is Product product && product.Unit is "g" or "ml")
            UnitCombo.SelectedItem = product.Unit;
    }

    private void QuantityBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddToPantry_Click(sender, e);
    }

    private void AddToPantry_Click(object sender, RoutedEventArgs e)
    {
        if (ProductCombo.SelectedItem is not Product product)
        {
            ShowStatus("Choose a product first.", isError: true);
            return;
        }

        if (!IngredientParser.TryParseQuantity(QuantityBox.Text, out double quantity))
        {
            ShowStatus("Enter a quantity greater than 0, for example 500, 1.5 or 1/2.", isError: true);
            return;
        }

        string unit = UnitCombo.SelectedItem as string ?? "g";
        string typed = QuantityBox.Text.Trim();
        if (!PantryService.Add(_items, product.ProductId, product.Name, quantity, unit))
        {
            ShowStatus("That quantity cannot be added.", isError: true);
            return;
        }

        if (!Save()) return;

        QuantityBox.Text = string.Empty;
        RefreshList();
        ShowStatus($"Added {typed} {unit} {product.Name} to the pantry.", isError: false);
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PantryItem item }) return;

        _items.Remove(item);
        if (!Save()) return;

        RefreshList();
        ShowStatus($"Removed {item.Name} from the pantry.", isError: false);
    }

    private bool Save()
    {
        try
        {
            _pantryService.Save(AppPaths.PantryPath, _items);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStatus($"The pantry could not be saved: {ex.Message}", isError: true);
            return false;
        }
    }

    private void RefreshList()
    {
        var sorted = _items.OrderBy(i => i.Name).ToList();
        PantryList.ItemsSource = null;
        PantryList.ItemsSource = sorted;
        EmptyText.Visibility = sorted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(string text, bool isError)
    {
        StatusText.Foreground = isError ? Brushes.Firebrick : (Brush)FindResource("PrimaryBrush");
        StatusText.Text = text;
    }
}
