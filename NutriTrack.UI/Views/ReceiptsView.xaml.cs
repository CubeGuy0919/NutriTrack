using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;

namespace NutriTrack.UI.Views;

public partial class ReceiptsView : UserControl
{
    private List<Receipt> _allReceipts = new();
    private Receipt? _selectedReceipt;
    private Receipt? _editingReceipt;
    private readonly ObservableCollection<ReceiptItem> _draftItems = new();

    public ReceiptsView()
    {
        InitializeComponent();
        DialogItemsGrid.ItemsSource = _draftItems;
        PopulateFilterCombos();
        LoadReceipts();
    }

    private void PopulateFilterCombos()
    {
        // Populate products in dialog combobox
        var products = AppServices.GetAllowedProducts();
        DialogProductCombo.Items.Clear();
        foreach (var p in products)
        {
            DialogProductCombo.Items.Add(p.Name);
        }
    }

    public void LoadReceipts()
    {
        _allReceipts = AppServices.Receipts.GetAllReceipts();
        UpdateStoreFilterOptions();
        ApplyFilter();
    }

    private void UpdateStoreFilterOptions()
    {
        string currentStore = (StoreFilterCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "All stores";

        StoreFilterCombo.Items.Clear();
        StoreFilterCombo.Items.Add(new ComboBoxItem { Content = "All stores", IsSelected = true });

        var stores = _allReceipts
            .Select(r => r.Store.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);

        foreach (var store in stores)
        {
            var item = new ComboBoxItem { Content = store };
            if (store.Equals(currentStore, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = true;
            }
            StoreFilterCombo.Items.Add(item);
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        StoreFilterCombo.SelectedIndex = 0;
        DateFilterCombo.SelectedIndex = 0;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (ReceiptsItemsControl is null) return;

        var query = _allReceipts.AsEnumerable();

        // 1. Text Search (Store or Product names)
        var searchText = SearchBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(r =>
                r.Store.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                r.Items.Any(i => i.ProductName.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        }

        // 2. Store Filter
        if (StoreFilterCombo?.SelectedItem is ComboBoxItem storeItem &&
            storeItem.Content is string store && store != "All stores")
        {
            query = query.Where(r => r.Store.Equals(store, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Date Filter
        if (DateFilterCombo?.SelectedItem is ComboBoxItem dateItem &&
            dateItem.Content is string dateChoice)
        {
            var now = DateTime.Today;
            if (dateChoice == "This month")
            {
                query = query.Where(r => r.PurchaseDate.Year == now.Year && r.PurchaseDate.Month == now.Month);
            }
            else if (dateChoice == "Last month")
            {
                var lastMonth = now.AddMonths(-1);
                query = query.Where(r => r.PurchaseDate.Year == lastMonth.Year && r.PurchaseDate.Month == lastMonth.Month);
            }
            else if (dateChoice == "Last 30 days")
            {
                var cutoff = now.AddDays(-30);
                query = query.Where(r => r.PurchaseDate >= cutoff);
            }
        }

        var filtered = query.ToList();
        ReceiptsItemsControl.ItemsSource = filtered;

        // Update stats
        int count = filtered.Count;
        ResultSummaryText.Text = count == 1 ? "1 receipt found" : $"{count} receipts found";
        decimal totalSpending = filtered.Sum(r => r.TotalAmount);
        TotalSpendingText.Text = $"Total: {totalSpending:N0} Ft";

        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // If selected receipt is not in filtered list, hide details
        if (_selectedReceipt != null && !filtered.Any(r => r.ReceiptId == _selectedReceipt.ReceiptId))
        {
            CloseDetail();
        }
    }

    // -----------------------------------------------------------------
    // Receipt Details Panel
    // -----------------------------------------------------------------

    private void ReceiptCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Receipt receipt) return;
        ShowDetail(receipt);
    }

    private void ShowDetail(Receipt receipt)
    {
        _selectedReceipt = receipt;
        DetailStoreText.Text = receipt.Store;
        DetailDateText.Text = receipt.PurchaseDate.ToString("MMMM d, yyyy");
        DetailTotalText.Text = $"{receipt.TotalAmount:N0} Ft";
        DetailItemCountText.Text = $"{receipt.Items.Count} items";
        DetailItemsList.ItemsSource = receipt.Items;

        DetailBorder.Visibility = Visibility.Visible;
        DetailColumn.Width = new GridLength(360);
    }

    private void CloseDetail_Click(object sender, RoutedEventArgs e)
    {
        CloseDetail();
    }

    private void CloseDetail()
    {
        _selectedReceipt = null;
        DetailBorder.Visibility = Visibility.Collapsed;
        DetailColumn.Width = new GridLength(0);
    }

    private void DeleteSelectedReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReceipt == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the receipt from '{_selectedReceipt.Store}' ({_selectedReceipt.PurchaseDate:yyyy-MM-dd})?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            AppServices.Receipts.DeleteReceipt(_selectedReceipt.ReceiptId);
            CloseDetail();
            LoadReceipts();
        }
    }

    private void EditSelectedReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReceipt == null) return;
        OpenReceiptDialog(_selectedReceipt);
    }

    // -----------------------------------------------------------------
    // Add / Edit Modal Dialog
    // -----------------------------------------------------------------

    private void AddReceipt_Click(object sender, RoutedEventArgs e)
    {
        OpenReceiptDialog(null);
    }

    private void OpenReceiptDialog(Receipt? receiptToEdit)
    {
        _editingReceipt = receiptToEdit;
        _draftItems.Clear();

        if (receiptToEdit != null)
        {
            ModalTitleText.Text = "Edit Receipt";
            DialogStoreInput.Text = receiptToEdit.Store;
            DialogDatePicker.SelectedDate = receiptToEdit.PurchaseDate;

            foreach (var item in receiptToEdit.Items)
            {
                _draftItems.Add(new ReceiptItem
                {
                    ReceiptItemId = item.ReceiptItemId,
                    ReceiptId = item.ReceiptId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }
        }
        else
        {
            ModalTitleText.Text = "Add New Receipt";
            DialogStoreInput.Text = string.Empty;
            DialogDatePicker.SelectedDate = DateTime.Today;
        }

        DialogProductCombo.Text = string.Empty;
        DialogQuantityInput.Text = "1";
        DialogUnitPriceInput.Text = string.Empty;
        UpdateDialogTotal();

        AddReceiptModal.Visibility = Visibility.Visible;
    }

    private void DialogProductCombo_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (DialogProductCombo.SelectedItem is string productName)
        {
            DialogProductCombo.Text = productName;
        }
    }

    private void DialogAddItem_Click(object sender, RoutedEventArgs e)
    {
        string productName = DialogProductCombo.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(productName))
        {
            MessageBox.Show("Please enter or select a product name.", "Missing Product", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!double.TryParse(DialogQuantityInput.Text?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double qty) || qty <= 0)
        {
            MessageBox.Show("Please enter a valid positive quantity.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string priceText = DialogUnitPriceInput.Text?.Trim().Replace("Ft", "").Replace(",", "").Trim() ?? "0";
        if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal unitPrice) || unitPrice < 0)
        {
            MessageBox.Show("Please enter a valid unit price (e.g. 450).", "Invalid Price", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Match existing product ID if found in catalog
        var matchedProduct = AppServices.GetAllowedProducts()
            .FirstOrDefault(p => p.Name.Equals(productName, StringComparison.OrdinalIgnoreCase));

        _draftItems.Add(new ReceiptItem
        {
            ReceiptItemId = _draftItems.Count + 1,
            ProductId = matchedProduct?.ProductId ?? 0,
            ProductName = productName,
            Quantity = qty,
            UnitPrice = unitPrice
        });

        // Reset inputs
        DialogProductCombo.Text = string.Empty;
        DialogQuantityInput.Text = "1";
        DialogUnitPriceInput.Text = string.Empty;

        UpdateDialogTotal();
    }

    private void DialogRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ReceiptItem item)
        {
            _draftItems.Remove(item);
            UpdateDialogTotal();
        }
    }

    private void UpdateDialogTotal()
    {
        decimal total = _draftItems.Sum(i => i.LineTotal);
        DialogTotalText.Text = $"{total:N0} Ft";
    }

    private void SaveReceiptDialog_Click(object sender, RoutedEventArgs e)
    {
        string store = DialogStoreInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(store))
        {
            MessageBox.Show("Please enter a store name (e.g. Tesco, Spar, Lidl).", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_draftItems.Count == 0)
        {
            MessageBox.Show("Please add at least one item to the receipt.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DateTime date = DialogDatePicker.SelectedDate ?? DateTime.Today;

        if (_editingReceipt != null)
        {
            _editingReceipt.Store = store;
            _editingReceipt.PurchaseDate = date;
            _editingReceipt.Items = _draftItems.ToList();
            _editingReceipt.RecalculateTotal();
            AppServices.Receipts.UpdateReceipt(_editingReceipt);

            if (_selectedReceipt?.ReceiptId == _editingReceipt.ReceiptId)
            {
                ShowDetail(_editingReceipt);
            }
        }
        else
        {
            var newReceipt = new Receipt
            {
                Store = store,
                PurchaseDate = date,
                Items = _draftItems.ToList()
            };
            newReceipt.RecalculateTotal();
            AppServices.Receipts.AddReceipt(newReceipt);
        }

        AddReceiptModal.Visibility = Visibility.Collapsed;
        LoadReceipts();
    }

    private void CancelReceiptDialog_Click(object sender, RoutedEventArgs e)
    {
        AddReceiptModal.Visibility = Visibility.Collapsed;
    }
}
