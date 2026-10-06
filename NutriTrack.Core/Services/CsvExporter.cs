using System.Globalization;
using System.Text;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>RFC 4180 CSV writer: quotes fields containing commas, quotes or line breaks, and defuses spreadsheet formulas.</summary>
public static class CsvExporter
{
    /// <summary>
    /// Makes one TEXT value safe for a CSV file.
    /// Step 1: if it starts with = + - @ a spreadsheet could run it as a formula, so a ' is put in front.
    /// Step 2: if it contains a comma, quote or line break, wrap it in quotes and double any quotes inside.
    /// </summary>
    public static string Cell(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        char first = text[0];
        bool looksLikeFormula = first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r';
        if (looksLikeFormula) text = "'" + text;

        bool needsQuotes = text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
        if (!needsQuotes) return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Num(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Date(DateTime d) => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Build(string[] header, IEnumerable<string[]> rows)
    {
        // Rows are already safe: callers pass text through Cell(), and numbers/dates need no escaping.
        var sb = new StringBuilder();
        sb.Append(string.Join(",", header.Select(Cell))).Append("\r\n");
        foreach (var r in rows) sb.Append(string.Join(",", r)).Append("\r\n");
        return sb.ToString();
    }

    public static string Consumption(IEnumerable<ConsumptionEvent> events, DateRange range) => Build(
        new[] { "Date", "Food", "Category", "Quantity", "Unit", "Calories", "Protein_g", "Carbs_g", "Fat_g", "Fiber_g", "Sugar_g", "Salt_g", "Recipe", "Meal" },
        events.Where(e => range.Contains(e.Timestamp)).OrderBy(e => e.Timestamp).Select(e => new[]
        {
            Date(e.Timestamp), Cell(e.ProductName), Cell(e.Category), Num(e.Quantity), Cell(e.Unit), Num(e.Calories),
            Num(e.Protein), Num(e.Carbs), Num(e.Fat), Num(e.Fiber), Num(e.Sugar), Num(e.Salt), Cell(e.RecipeName), Cell(e.Meal)
        }));

    /// <summary>Daily nutrition totals (days with at least one logged event).</summary>
    public static string NutritionHistory(IEnumerable<ConsumptionEvent> events, DateRange range) => Build(
        new[] { "Date", "Entries", "Calories", "Protein_g", "Carbs_g", "Fat_g", "Fiber_g", "Sugar_g", "Salt_g" },
        events.Where(e => range.Contains(e.Timestamp)).GroupBy(e => e.Timestamp.Date).OrderBy(g => g.Key).Select(g => new[]
        {
            g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Count().ToString(CultureInfo.InvariantCulture),
            Num(g.Sum(e => e.Calories)), Num(g.Sum(e => e.Protein)), Num(g.Sum(e => e.Carbs)), Num(g.Sum(e => e.Fat)),
            Num(g.Sum(e => e.Fiber)), Num(g.Sum(e => e.Sugar)), Num(g.Sum(e => e.Salt))
        }));

    public static string Spending(IEnumerable<Receipt> receipts, DateRange range) => Build(
        new[] { "Date", "Store", "Product", "Quantity", "UnitPrice", "LineTotal", "ReceiptTotal" },
        receipts.Where(r => range.Contains(r.PurchaseDate)).OrderBy(r => r.PurchaseDate)
                .SelectMany(r => r.Items.Select(i => new[]
                {
                    Date(r.PurchaseDate), Cell(r.Store), Cell(i.ProductName), Num(i.Quantity), Num(i.UnitPrice), Num(i.LineTotal), Num(r.TotalAmount)
                })));

    public static string Waste(IEnumerable<WasteEvent> events, DateRange range) => Build(
        new[] { "Date", "Product", "Category", "Quantity", "Unit", "Reason", "EstimatedValue" },
        events.Where(w => range.Contains(w.Timestamp)).OrderBy(w => w.Timestamp).Select(w => new[]
        {
            Date(w.Timestamp), Cell(w.ProductName), Cell(w.Category), Num(w.Quantity), Cell(w.Unit), w.Reason.ToString(),
            w.EstimatedValue.HasValue ? Num(w.EstimatedValue.Value) : string.Empty
        }));

    public static string Pantry(IEnumerable<PantryItem> items) => Build(
        new[] { "Product", "Category", "Quantity", "Unit", "ExpiryDate" },
        items.OrderBy(i => i.ProductName).Select(i => new[]
        {
            Cell(i.ProductName), Cell(i.Category), Num(i.Quantity), Cell(i.Unit),
            i.ExpiryDate.HasValue ? i.ExpiryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty
        }));

    public static string Cooking(IEnumerable<CookingEvent> events, DateRange range) => Build(
        new[] { "Date", "Recipe", "Cuisine", "Category", "Servings", "Calories", "NutritionLogged", "PantryItemsDeducted", "UnmatchedIngredients" },
        events.Where(e => range.Contains(e.Timestamp)).OrderBy(e => e.Timestamp).Select(e => new[]
        {
            Date(e.Timestamp), Cell(e.RecipeName), Cell(e.Cuisine), Cell(e.Category), e.Servings.ToString(CultureInfo.InvariantCulture),
            Num(e.Calories), e.NutritionLogged ? "yes" : "no", e.PantryDeductions.Count.ToString(CultureInfo.InvariantCulture),
            Cell(string.Join("; ", e.UnmatchedIngredients))
        }));

    /// <summary>Writes UTF-8 with BOM so Excel shows accented characters correctly.</summary>
    public static void WriteFile(string path, string csv) => File.WriteAllText(path, csv, new UTF8Encoding(true));
}
