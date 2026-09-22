namespace NutriTrack.Core.Models;

public enum ExpiryStatus
{
    Normal,
    ExpiringSoon,
    Expired,
    NoDate
}

public class PantryItem
{
    public int PantryItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double Quantity { get; set; } = 1;
    public string Unit { get; set; } = "pcs";
    public DateTime? ExpiryDate { get; set; }

    public ExpiryStatus ExpiryStatus
    {
        get
        {
            if (!ExpiryDate.HasValue) return ExpiryStatus.NoDate;
            var today = DateTime.Today;
            var expDate = ExpiryDate.Value.Date;
            if (expDate < today) return ExpiryStatus.Expired;
            if ((expDate - today).TotalDays <= 3) return ExpiryStatus.ExpiringSoon;
            return ExpiryStatus.Normal;
        }
    }

    public string ExpiryText
    {
        get
        {
            if (!ExpiryDate.HasValue) return "No expiry date";
            var today = DateTime.Today;
            var expDate = ExpiryDate.Value.Date;
            int days = (int)(expDate - today).TotalDays;

            if (days < 0) return "Expired";
            if (days == 0) return "Expires today";
            if (days == 1) return "Expires tomorrow";
            return $"Expires in {days} days";
        }
    }

    public string FormattedQuantity => $"{Quantity:0.##} {Unit}".Trim();

    public override string ToString() => $"{ProductName} ({FormattedQuantity})";
}
