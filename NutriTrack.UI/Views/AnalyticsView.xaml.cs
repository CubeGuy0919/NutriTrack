using System.Windows.Controls;

namespace NutriTrack.UI.Views;

/// <summary>
/// The Analytics screen. This file only does 3 things:
///   1. ask AnalyticsService for the numbers,
///   2. put the numbers into the text boxes and charts,
///   3. reload everything when the user changes the time range.
/// No calculations here, they all live in AnalyticsService (Core).
/// </summary>
public partial class AnalyticsView : UserControl
{
    // The dropdown shows 3 choices; these are the matching "number of months".
    private readonly int[] _monthOptions = { 3, 6, 12 };

    public AnalyticsView()
    {
        InitializeComponent();

        // Fill the dropdown and pick "Last 6 months".
        RangeCombo.Items.Add("Last 3 months");
        RangeCombo.Items.Add("Last 6 months");
        RangeCombo.Items.Add("Last 12 months");
        RangeCombo.SelectedIndex = 1;

        // Subscribe AFTER setting the default, so it does not fire during setup.
        RangeCombo.SelectionChanged += (sender, e) => LoadAnalytics();

        LoadAnalytics();
    }

    private void LoadAnalytics()
    {
        int months = _monthOptions[RangeCombo.SelectedIndex];

        // 1. Get the raw data from the shared services.
        var analytics = AppServices.Analytics;
        var allReceipts = AppServices.Receipts.GetAllReceipts();
        var pantryItems = AppServices.Pantry.GetAllPantryItems();
        var recipes = AppServices.GetLoadedRecipes();

        // 2. Keep only the receipts inside the chosen time range.
        var receipts = analytics.FilterByMonths(allReceipts, months);

        // 3. Show the three big numbers.
        TotalSpentText.Text = $"{analytics.GetTotalSpent(receipts):N0} Ft";
        ReceiptCountText.Text = receipts.Count.ToString();
        AverageReceiptText.Text = $"{analytics.GetAverageReceipt(receipts):N0} Ft";

        // 4. Show the insights and each chart (ItemsSource = "the list to draw").
        InsightsList.ItemsSource = analytics.GetInsights(receipts, pantryItems);
        MonthlyChart.ItemsSource = analytics.GetSpendingByMonth(receipts, months);
        StoreChart.ItemsSource = analytics.GetSpendingByStore(receipts);
        TopProductsChart.ItemsSource = analytics.GetTopProducts(receipts);
        FreshnessChart.ItemsSource = analytics.GetPantryFreshness(pantryItems);
        CategoryChart.ItemsSource = analytics.GetRecipesByCategory(recipes);
    }
}
