# NutriTrack – latest requested changes

## Receipt expiry date
- The Add/Edit Receipt dialog now includes an optional expiry-date textbox for each receipt item.
- Enter dates as `yyyy-MM-dd` (for example, `2026-11-20`). The dialog also accepts `yyyy.MM.dd`, `yyyy/MM/dd`, and `dd.MM.yyyy`.
- Leave the field empty if the package has no readable expiry date.
- The date is saved with the receipt item, shown in the receipt item grid, and displayed in receipt details.
- The app does not guess expiry dates. The user enters the date printed on the package.

## Mark a recipe as made
- Removed the Daily Menu navigation option and its separate Menu window.
- The Recipe details panel now has a **I Made This** button.
- Clicking it records one serving with the current timestamp and nutrition snapshot in `MealLog.txt`.
- Meal type is inferred from the recipe category (Breakfast/Bread, Lunch/Soup/Salad, Dinner/Meal; other categories are logged as Snack).
- Statistics reads this meal log for its eating-habit statistics and charts.

## Build
Run on Windows with the .NET 10 SDK:

```powershell
dotnet restore
dotnet build .\NutriTrack.slnx
dotnet run --project .\NutriTrack.UI\NutriTrack.UI.csproj
```
