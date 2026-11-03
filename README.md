# NutriTrack

A desktop app (C# / WPF / .NET 10, Windows) that helps a household answer one question:
**"What can I cook with what I have, and what do I still need to buy?"**

## Run it
```
dotnet run --project NutriTrack.UI
```
(or open `NutriTrack.slnx` / `NutriTrack.sln` and press F5). SDK: .NET 10, see `global.json`.

## The two projects
| Project | What is in it | Knows about WPF? |
|---|---|---|
| `NutriTrack.Core` | `Models/` (plain data classes) and `Services/` (all the logic) | no |
| `NutriTrack.UI` | `Views/` (screens), `Converters/`, `Styles/`, `Data/` (Recipes.txt, Products.txt), `Assets/Images/` | yes |

Rule of thumb: **if it is a calculation or a file format it is in Core, if you can click it it is in UI.**

## The screens
| Screen | What it does |
|---|---|
| Home | receipts this month, recipe of the day (best pantry match), "use soon" list for food about to expire |
| Recipes | search / filter / sort, add a recipe, detail panel with servings slider, unit switch and pantry check |
| Receipts | add / edit / delete shopping receipts (`Receipts.json`) |
| Pantry | what is at home, with quantity and expiry date (`Pantry.json`), "what can I cook?" |
| Shopping | list of missing ingredients (`ShoppingList.txt`); "Bought" moves an item into the pantry |

## Code tour (what to explain in the presentation)
1. `Recipes.txt` is read by `RecipeService` into `Recipe` objects. Every ingredient line ("600 g ripe tomatoes")
   is turned into an `IngredientItem` (quantity 600, unit g, name) by `IngredientParser`.
2. The servings slider calls `IngredientScaler`, which does three steps **in this order**:
   **scale** (x selected/base servings) -> **convert** (unit system) -> **format** (text).
3. `RecipeAvailabilityCalculator` compares the scaled ingredients with the pantry
   (`UnitConverter` brings everything to g / ml / pieces first, so 1 kg in the pantry covers 750 g).
4. "Add missing ingredients" -> `ShoppingListService`; "Bought" -> `PantryService.AddOrIncrease`.
5. `AppServices` holds the one shared pantry / receipts / shopping list, so all screens agree.

## Unit switch (As written / Metric / Imperial)
All unit code is in one file: `NutriTrack.Core/Services/Units.cs`. It only changes how amounts are **shown**;
pantry and shopping maths never use the display system.

| Mode | What happens |
|---|---|
| As written | nothing is converted |
| Metric | oz, lb -> g / kg; tsp, tbsp, cup, fl oz, dl, cl -> ml / l |
| Imperial | g, kg -> oz / lb; ml, l, dl, cl -> fl oz (tsp, tbsp and cups are already imperial) |

Counted units (pcs, clove, slice, pinch ...) are never converted.

## Data files (next to the .exe)
`Data/Recipes.txt` and `Data/Products.txt` are shipped. `Pantry.json`, `Receipts.json` and `ShoppingList.txt` are created
on first use (the first run fills pantry and receipts with demo data). A clean rebuild removes them.


## Recipe Explorer

The Recipes screen is the single recipe workspace. It includes local recipe search, cuisine/time sorting, favorites, serving scaling, nutrition, pantry availability and shopping-list integration. Categories include Breakfast, Lunch, Dinner, Bread, Meal, Soup, Salad, Snack and Dessert.

The curated collection contains 250 recipes. The 50 additions (IDs 201–250) use only the supplied `Data/Products.txt` catalogue. Their per-serving calorie, protein, carbohydrate and fat values are calculated from the product nutrition fields. Milk/oil volume-to-mass assumptions are approximate and are disclosed in each recipe description.

Favorites and the 20 most recently viewed recipes are saved outside the installation directory under `%LOCALAPPDATA%\NutriTrack`. The “Cook with my pantry” filter uses the existing pantry and ingredient matching services. Recipe cards also show per-serving macro summaries when available.

## Full recipe-derived product catalogue

`NutriTrack.UI/Data/Products.txt` is the shared Pantry/Recipes/Shopping/Receipts product catalogue. It contains 4,950 normalized ingredient entries extracted from the `ingredients` JSON field of the Hugging Face `datahiveai/recipes-with-nutrition` dataset (39,447 recipes; dataset URL: https://huggingface.co/datasets/datahiveai/recipes-with-nutrition). The 9,725 distinct ingredient strings in that source are covered by the catalogue's case-, accent-, punctuation- and plural-tolerant matcher.

The original 20 curated products retain their existing nutrition values. Other entries have blank per-100g nutrition because the source dataset contains recipe-level totals, not reliable per-ingredient nutrient profiles. NutriTrack treats blank values as unknown and will not use them to fabricate recipe nutrition calculations. Product categories are organizational classifications inferred from ingredient names, not source-provided dietary facts.
