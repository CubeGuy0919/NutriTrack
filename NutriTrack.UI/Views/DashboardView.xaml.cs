using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NutriTrack.Core.Models;
using NutriTrack.Core.Services;

namespace NutriTrack.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly RecipeService _recipeService = new();
    private readonly ProductService _productService = new();

    public List<Recipe> LoadedRecipes { get; private set; } = new();
    public List<Product> AllowedProducts { get; private set; } = new();

    public DashboardView()
    {
        InitializeComponent();
        LoadAllLocalData();
    }

    private void LoadAllLocalData()
    {
        MessageBox.Show("Start Loading Datas");
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 1. Load Recipes
        string recipePath = Path.Combine(baseDir, "Recipes.txt");
        if (File.Exists(recipePath))
        {
            LoadedRecipes = _recipeService.LoadRecipesFromTxt(recipePath);
            MessageBox.Show("Successful Loading");
        }
        else
        {
            MessageBox.Show("Unsuccessful Loading");
        }

        // 2. Load Allowed Products
        string productPath = Path.Combine(baseDir, "Products.txt");
        if (File.Exists(productPath))
        {
            AllowedProducts = _productService.LoadAllowedProducts(productPath);
        }
    }
}