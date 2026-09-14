namespace NutriTrack.Core.Models;

public class Receipt
{
    public int ReceiptId { get; set; }
    public int UserId { get; set; }
    public string Store { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }

    public List<ReceiptItem> Items { get; set; } = new();

    /// <summary>Recalculates TotalAmount from the current line items.</summary>
    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.LineTotal);
    }
}

public class ReceiptItem
{
    public int ReceiptItemId { get; set; }
    public int ReceiptId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * (decimal)Quantity;
}
