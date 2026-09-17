# NutriTrack — starter solution

## What's here
- **NutriTrack.UI** — WPF app (.NET 10). Sidebar navigation + a Dashboard view with placeholder tiles, and stub views for Receipts/Pantry/Shopping/Analytics.
- **NutriTrack.Core** — plain C# models (`Product`, `Receipt`, `ReceiptItem`) with no dependencies. This is where business logic/services will live as you build them out.
- **NutriTrack.Data** — MySQL access layer using `MySqlConnector`. Has a `DatabaseConnection` helper and a first `ProductRepository` (GetAll/Insert).

## Opening it in VS Code
1. Unzip, then open the **NutriTrack** folder in VS Code (`File → Open Folder`).
2. Install the recommended extensions when prompted (C# Dev Kit). VS Code will restore NuGet
   packages on first build — that needs internet, to pull down `MySqlConnector`.
3. Press **F5** and pick *Launch NutriTrack.UI (Debug)*. You should get a window with a green
   sidebar and a Dashboard with three placeholder tiles.
4. From a terminal instead: `dotnet run --project NutriTrack.UI`.

WPF is Windows-only, so the UI project builds and runs on Windows only. `NutriTrack.Core` and
`NutriTrack.Data` are plain `net10.0` and build anywhere.

## Opening it in Visual Studio
`NutriTrack.sln` still works — double-click it, set **NutriTrack.UI** as the startup project, F5.
