using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

public class ReceiptService
{
    private readonly string _filePath;
    private readonly List<Receipt> _receipts = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Message of the last failed save, or null when the last save worked.</summary>
    public string? LastSaveError { get; private set; }

    public ReceiptService(string? customPath = null)
    {
        _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts.json");
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
                string json = File.ReadAllText(_filePath);
                var items = JsonSerializer.Deserialize<List<Receipt>>(json, _jsonOptions);
                if (items != null)
                {
                    foreach (var r in items)
                    {
                        r.RecalculateTotal();
                        _receipts.Add(r);
                    }
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable file: fall back to the demo receipts (the broken file is kept as .bak on the next save)
        }

        // Initialize with default demo receipts if file doesn't exist
        SeedInitialReceipts();
        SaveReceipts();
    }

    public void SaveReceipts()
    {
        try
        {
            string json = JsonSerializer.Serialize(_receipts, _jsonOptions);
            TextFile.WriteAtomic(_filePath, json);
            LastSaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
        }
    }

    private void SeedInitialReceipts()
    {
        var now = DateTime.Today;

        var r1 = new Receipt
        {
            ReceiptId = 1,
            Store = "TESCO",
            PurchaseDate = now.AddDays(-2),
            Items = new List<ReceiptItem>
            {
                new() { ReceiptItemId = 1, ReceiptId = 1, ProductId = 1, ProductName = "Chicken Breast", Quantity = 1, UnitPrice = 1890 },
                new() { ReceiptItemId = 2, ReceiptId = 1, ProductId = 4, ProductName = "Whole Milk", Quantity = 2, UnitPrice = 449 },
                new() { ReceiptItemId = 3, ReceiptId = 1, ProductId = 7, ProductName = "Eggs", Quantity = 10, UnitPrice = 85 },
                new() { ReceiptItemId = 4, ReceiptId = 1, ProductId = 8, ProductName = "White Rice (Uncooked)", Quantity = 1, UnitPrice = 690 }
            }
        };
        r1.RecalculateTotal();

        var r2 = new Receipt
        {
            ReceiptId = 2,
            Store = "SPAR",
            PurchaseDate = now.AddDays(-7),
            Items = new List<ReceiptItem>
            {
                new() { ReceiptItemId = 5, ReceiptId = 2, ProductId = 3, ProductName = "Salmon Fillet", Quantity = 1, UnitPrice = 2990 },
                new() { ReceiptItemId = 6, ReceiptId = 2, ProductId = 5, ProductName = "Greek Yogurt (Plain)", Quantity = 3, UnitPrice = 320 },
                new() { ReceiptItemId = 7, ReceiptId = 2, ProductId = 9, ProductName = "Oats", Quantity = 1, UnitPrice = 499 }
            }
        };
        r2.RecalculateTotal();

        var r3 = new Receipt
        {
            ReceiptId = 3,
            Store = "LIDL",
            PurchaseDate = now.AddDays(-15),
            Items = new List<ReceiptItem>
            {
                new() { ReceiptItemId = 8, ReceiptId = 3, ProductId = 6, ProductName = "Cheddar Cheese", Quantity = 1, UnitPrice = 1250 },
                new() { ReceiptItemId = 9, ReceiptId = 3, ProductId = 2, ProductName = "Ground Beef (80/20)", Quantity = 1, UnitPrice = 2190 }
            }
        };
        r3.RecalculateTotal();

        _receipts.Add(r1);
        _receipts.Add(r2);
        _receipts.Add(r3);
    }
}
