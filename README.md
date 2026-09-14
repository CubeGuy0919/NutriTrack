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

## Before the DB actually works
`DatabaseConnection.CreateLocalDefault()` currently points at:
```
Server=localhost; Database=nutritrack; UserID=root; Password=changeme
```
Update those values (or better, move them into an `appsettings.json` / user secret before you submit — hardcoded credentials are an easy thing for a grader to flag). You'll also need to actually create the `nutritrack` database and a `Products` table matching the columns in `ProductRepository.cs` — this matches the schema in the project spec doc.

## Suggested next coding steps (matches the Trello board)
1. Create the MySQL schema (`Products`, `Users`, `Receipts`, `ReceiptItems`, `PantryItems`) — see the ERD in the project spec.
2. Wire `DashboardView`'s tiles to real repository calls instead of hardcoded "0".
3. Build the Receipt add/edit screen (`Views/ReceiptEditView.xaml`) — this is next on the board.
4. Add a `UserRepository` + login screen before anything else needs a real `UserId`.
