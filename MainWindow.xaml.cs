using System.Windows;
using NutriTrack.UI.Views;

namespace NutriTrack.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new DashboardView();
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new DashboardView();

    private void NavRecipes_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new RecipesView();

    private void NavReceipts_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new ReceiptsView();

    private void NavPantry_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new PantryView();

    private void NavShopping_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new ShoppingListView();

    private void NavAnalytics_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new PlaceholderView("Analytics", "Spending and nutrition charts land here.");

    /// <summary>Shows the Recipes screen with the given recipe already open (used by the Dashboard and the Pantry).</summary>
    public void OpenRecipe(int recipeId) =>
        MainContent.Content = new RecipesView(recipeId);
}
