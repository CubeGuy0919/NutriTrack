# NutriTrack — starter solution

## What's here
- **NutriTrack.UI** — WPF app (.NET 8). Sidebar navigation + a Dashboard view with placeholder tiles, and stub views for Receipts/Pantry/Shopping/Analytics.
- **NutriTrack.Core** — plain C# models (`Product`, `Receipt`, `ReceiptItem`) with no dependencies. This is where business logic/services will live as you build them out.
- **NutriTrack.Data** — MySQL access layer using `MySqlConnector`. Has a `DatabaseConnection` helper and a first `ProductRepository` (GetAll/Insert).

## Opening it in Visual Studio 2026
1. Unzip, then double-click `NutriTrack.sln`.
2. Visual Studio will restore NuGet packages automatically on first build (needs internet — it needs to pull down `MySqlConnector`).
3. Set **NutriTrack.UI** as the startup project (right-click it → *Set as Startup Project*) if it isn't already.
4. Press F5. You should get a window with a green sidebar and a Dashboard with three placeholder tiles.
