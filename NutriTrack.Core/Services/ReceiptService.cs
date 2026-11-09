using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class ReceiptService
{
    private readonly string _filePath;
    private readonly List<Receipt> _receipts = new();
    /// <summary>Message of the last failed save, or null when the last save worked.</summary>
    public string? LastSaveError { get; private set; }

    public ReceiptService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts.txt");
        LoadReceipts();
    }

    public List<Receipt> GetAllReceipts() => _receipts.OrderByDescending(r => r.PurchaseDate).ToList();

    public Receipt? GetReceiptById(int id) => _receipts.FirstOrDefault(r => r.ReceiptId == id);

    public void AddReceipt(Receipt receipt)
    {
        if (receipt.ReceiptId <= 0)
        {
            receipt.ReceiptId = _receipts.Count > 0 ? _receipts.Max(r => r.ReceiptId) + 1 : 1;
        }

        int itemId = 1;
        foreach (var item in receipt.Items)
        {
            if (item.ReceiptItemId <= 0)
            {
                item.ReceiptItemId = itemId++;
            }
            item.ReceiptId = receipt.ReceiptId;
        }

        receipt.RecalculateTotal();
        _receipts.Add(receipt);
        SaveReceipts();
    }

    public void UpdateReceipt(Receipt receipt)
    {
        var existing = _receipts.FirstOrDefault(r => r.ReceiptId == receipt.ReceiptId);
        if (existing != null)
        {
            existing.Store = receipt.Store;
            existing.PurchaseDate = receipt.PurchaseDate;
            existing.Items = receipt.Items;
            existing.RecalculateTotal();
            SaveReceipts();
        }
    }

    public bool DeleteReceipt(int receiptId)
    {
        var item = _receipts.FirstOrDefault(r => r.ReceiptId == receiptId);
        if (item != null)
        {
            _receipts.Remove(item);
            SaveReceipts();
            return true;
        }
        return false;
    }

    public int GetReceiptsCountThisMonth(DateTime? referenceDate = null)
    {
        var refDate = referenceDate ?? DateTime.Today;
        return _receipts.Count(r => r.PurchaseDate.Year == refDate.Year && r.PurchaseDate.Month == refDate.Month);
    }

    public decimal GetTotalSpendingThisMonth(DateTime? referenceDate = null)
    {
        var refDate = referenceDate ?? DateTime.Today;
        return _receipts
            .Where(r => r.PurchaseDate.Year == refDate.Year && r.PurchaseDate.Month == refDate.Month)
            .Sum(r => r.TotalAmount);
    }

    public void LoadReceipts()
    {
        _receipts.Clear();
        try
        {
            if (File.Exists(_filePath))
            {
                foreach (var line in File.ReadAllLines(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var f = TextFile.SplitRecord(line);
                    if (f[0] == "R" && f.Length == 6)
                    {
                        _receipts.Add(new Receipt
                        {
                            ReceiptId = int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture),
                            UserId = int.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                            Store = TextFile.DecodeField(f[3]),
                            PurchaseDate = DateTime.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                            TotalAmount = decimal.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture)
                        });
                    }
                    else if (f[0] == "I" && f.Length == 8)
                    {
                        int receiptId = int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture);
                        var receipt = _receipts.FirstOrDefault(r => r.ReceiptId == receiptId);
                        if (receipt == null) continue;
                        receipt.Items.Add(new ReceiptItem
                        {
                            ReceiptId = receiptId,
                            ReceiptItemId = int.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                            ProductId = int.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture),
                            ProductName = TextFile.DecodeField(f[4]),
                            Quantity = double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture),
                            UnitPrice = decimal.Parse(f[6], System.Globalization.CultureInfo.InvariantCulture),
                            ExpiryDate = TextFile.ParseNullableDate(f[7])
                        });
                    }
                }
                if (_receipts.Count > 0)
                {
                    foreach (var receipt in _receipts) receipt.RecalculateTotal();
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            _receipts.Clear();
            LastSaveError = ex.Message;
        }

        SeedInitialReceipts();
        SaveReceipts();
    }

    public void SaveReceipts()
    {
        try
        {
            var lines = new List<string>
            {
                "# NutriTrack receipt data | R|id|userId|store(base64)|purchaseDate(ISO)|total",
                "# Item data | I|receiptId|itemId|productId|name(base64)|quantity|unitPrice|expiry(ISO)"
            };
            foreach (var r in _receipts)
            {
                r.RecalculateTotal();
                lines.Add(string.Join("|", "R",
                    r.ReceiptId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    r.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    TextFile.EncodeField(r.Store),
                    r.PurchaseDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    r.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                foreach (var item in r.Items)
                {
                    lines.Add(string.Join("|", "I",
                        r.ReceiptId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.ReceiptItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        TextFile.EncodeField(item.ProductName),
                        item.Quantity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                        item.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.ExpiryDate?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }
            TextFile.WriteAtomic(_filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }
    }

    private void SeedInitialReceipts()
    {
        var today = DateTime.Today;
        int nextItemId = 1;

        Receipt Make(int id, string store, int daysAgo, params (int ProductId, string Name, double Qty, decimal Price)[] lines)
        {
            var receipt = new Receipt
            {
                ReceiptId = id,
                Store = store,
                PurchaseDate = today.AddDays(-daysAgo),
                Items = lines.Select(line => new ReceiptItem
                {
                    ReceiptItemId = nextItemId++,
                    ReceiptId = id,
                    ProductId = line.ProductId,
                    ProductName = line.Name,
                    Quantity = line.Qty,
                    UnitPrice = line.Price
                }).ToList()
            };
            receipt.RecalculateTotal();
            return receipt;
        }

        // Six months of realistic Hungarian supermarket history for useful analytics charts.
        _receipts.AddRange(new[]
        {
            Make(1, "TESCO", 2,
                (1, "Chicken Breast", 1, 1890m), (4, "Whole Milk", 2, 449m),
                (7, "Eggs", 10, 85m), (8, "White Rice (Uncooked)", 1, 690m),
                (13, "Banana", 1, 599m)),
            Make(2, "SPAR", 8,
                (3, "Salmon Fillet", 1, 2990m), (5, "Greek Yogurt (Plain)", 3, 320m),
                (9, "Oats", 1, 499m), (14, "Apple", 1, 699m)),
            Make(3, "LIDL", 18,
                (6, "Cheddar Cheese", 1, 1250m), (2, "Ground Beef (80/20)", 1, 2190m),
                (15, "Broccoli", 2, 499m), (17, "Potatoes", 2, 399m)),
            Make(4, "ALDI", 32,
                (1, "Chicken Breast", 1.2, 2190m), (10, "Whole Wheat Bread", 2, 599m),
                (11, "Olive Oil", 1, 1890m), (16, "Spinach", 1, 449m)),
            Make(5, "TESCO", 47,
                (4, "Whole Milk", 3, 459m), (7, "Eggs", 20, 79m),
                (12, "Butter", 1, 899m), (18, "Almonds", 1, 1290m)),
            Make(6, "SPAR", 65,
                (3, "Salmon Fillet", 2, 2790m), (5, "Greek Yogurt (Plain)", 2, 349m),
                (13, "Banana", 1.5, 549m), (14, "Apple", 1, 749m)),
            Make(7, "LIDL", 88,
                (2, "Ground Beef (80/20)", 1, 2290m), (8, "White Rice (Uncooked)", 2, 649m),
                (15, "Broccoli", 2, 469m), (19, "Peanut Butter", 1, 1190m)),
            Make(8, "TESCO", 112,
                (1, "Chicken Breast", 1, 1990m), (4, "Whole Milk", 2, 429m),
                (9, "Oats", 1, 479m), (20, "Dark Chocolate (70%)", 2, 699m)),
            Make(9, "ALDI", 143,
                (6, "Cheddar Cheese", 1, 1190m), (10, "Whole Wheat Bread", 2, 549m),
                (12, "Butter", 1, 849m), (17, "Potatoes", 2, 379m)),
            Make(10, "SPAR", 174,
                (3, "Salmon Fillet", 1, 2690m), (5, "Greek Yogurt (Plain)", 3, 299m),
                (13, "Banana", 1, 499m), (16, "Spinach", 2, 399m))
        });
    }
}
