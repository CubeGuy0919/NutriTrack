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
        MainContent.Content = new PlaceholderView("Receipts", "Add, browse and search your receipts here.");

    private void NavPantry_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new PlaceholderView("Pantry", "Your current food inventory will show up here.");

    private void NavShopping_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new PlaceholderView("Shopping", "Shopping lists, synced with your pantry.");

    private void NavAnalytics_Click(object sender, RoutedEventArgs e) =>
        MainContent.Content = new PlaceholderView("Analytics", "Spending and nutrition charts land here.");
}