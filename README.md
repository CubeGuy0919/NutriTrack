# NutriTrack v8

Combines the two lines of work: **v6** (structured ingredients, add-recipe, serving scaling, shopping list) and
**v7** (receipts, pantry with expiry dates, dashboard).

## Projects
- **NutriTrack.UI** - WPF app (.NET 10, Windows only). Sidebar: Home, Recipes, Receipts, Pantry, Shopping, Analytics (placeholder).
- **NutriTrack.Core** - models and services, no UI dependencies.
- **NutriTrack.Data** - `Products.txt`, `Recipes.txt` and empty repository stubs for a future database.

## What each screen does
- **Home** - receipts this month, recipe count, recipe of the day (best pantry match), "use soon" list for items about to expire.
- **Recipes** - search / filter, add new recipes, detail panel with image slideshow, servings slider (1-12), live ingredient and
  nutrition scaling, unit switch (as written / metric / imperial), per-ingredient pantry status and
  "Add missing ingredients to shopping list".
- **Receipts** - add, edit, delete, filter receipts (`Receipts.json`).
- **Pantry** - stock with quantity, unit and expiry date (`Pantry.json`), "what can I cook?" suggestions.
- **Shopping** - the list built from recipes (`ShoppingList.txt`); "Bought" moves an item into the pantry.

## How the pieces fit
- Ingredients are structured (`IngredientItem`: quantity, unit, product). Everything below uses them.
- `IngredientScaler` -> `UnitConverter` / `QuantityFormatter`: scale first, then convert units, then format. Calculations
  (pantry, shopping) always use the recipe's own unit, so the unit switch only changes how things are shown.
- `RecipeNutritionCalculator`: calories / protein / carbs / fat in the recipe are **per serving**; totals = per serving x servings.
- `RecipeAvailabilityCalculator` compares the scaled ingredients with `PantryService` (g, ml and counts are converted, so 1 kg covers 750 g).
- `AppServices` holds the single shared pantry, receipts, shopping list, products and recipes.

## Data files (next to the .exe)
`Recipes.txt`, `Products.txt` (shipped), `Pantry.json`, `Receipts.json`, `ShoppingList.txt` (created on first use; the first
run fills the pantry and receipts with demo data). A clean rebuild removes files created at run time.

## Build
Open `NutriTrack.sln` (or `.slnx`) in Visual Studio 2026 / `dotnet run --project NutriTrack.UI` on Windows. SDK: .NET 10 (see `global.json`).
