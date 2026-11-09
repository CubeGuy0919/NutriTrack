# NutriTrack

A desktop app (C# / WPF / .NET 10, Windows) that helps a household answer one question:
**"What can I cook with what I have, and what do I still need to buy?"**

## Run it
```
dotnet run --project NutriTrack.UI
```
(or open `NutriTrack.slnx` and press F5). SDK: .NET 10, see `global.json`.

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
| Recipes | browse recipes, inspect ingredients and nutrition, and use "I Made This" to record meals in `MealLog.txt` |
| Receipts | add / edit / delete shopping receipts (`Receipts.txt`) |
| My Pantry | what is at home, with quantity and expiry date (`My Pantry.txt`), "what can I cook?" |
| Shopping | list of missing ingredients (`ShoppingList.txt`); "Bought" moves an item into the pantry |
| Statistics | spending, pantry freshness and recipe charts, plus **Eating habits**: calories per day, average per day / meal, meals by type, most eaten recipes, cuisines, weekday pattern, macro split, written insights |

## Code tour (what to explain in the presentation)
1. `Recipes.txt` is read by `RecipeService` into `Recipe` objects. Every ingredient line ("600 g ripe tomatoes")
   is turned into an `IngredientItem` (quantity 600, unit g, name) by `IngredientParser`.
2. The servings slider calls `IngredientScaler`, which does three steps **in this order**:
   **scale** (x selected/base servings) -> **convert** (unit system) -> **format** (text).
3. `RecipeAvailabilityCalculator` compares the scaled ingredients with the pantry
   (`UnitConverter` brings everything to g / ml / pieces first, so 1 kg in the pantry covers 750 g).
4. "Add missing ingredients" -> `ShoppingListService`; "Bought" -> `My PantryService.AddOrIncrease`.
5. `AppServices` holds the one shared pantry / receipts / shopping list, so all screens agree.

## Menu recommendation (Recipes screen)
`MenuRecommender` (Core) builds one meal per slot. Each slot only looks at its own recipe categories
(breakfast: Breakfast + Bread, lunch: Lunch + Soup + Salad, dinner: Dinner + Meal, snack: Snack + Dessert) and scores every recipe:

| Rule | Points |
|---|---|
| ingredients already in the pantry (expired food does not count) | up to 40 |
| uses food that expires within 3 days | +20 |
| calories fit the slot's share of the daily target (25 / 30 / 35 / 10 %); 1 or 2 servings, whichever is closer | up to 25 |
| favorite recipe / ready in 30 minutes or less | +10 / +5 |
| not eaten in the last 7 days (from the meal log) / eaten | +10 / -25 |

The best recipe of each slot wins; "Suggest another menu" picks one of the 5 best instead. Every card shows *why* it was chosen.
The diet filter uses the `FoodSensitivities` tags of `Recipes.txt` (Vegan, Gluten-Free, Dairy-Free).

## Meal log and eating statistics
`MealLogService` stores every "I Made This" action in `MealLog.txt` (recipe, meal type, time, servings and a snapshot of calories / protein / carbs / fat).
On the very first run it creates about 3 weeks of demo meals so the charts are not empty (same idea as the demo pantry).
`StatisticsService` turns the log into the Eating habits statistics. Only about one recipe in five has macro values, so the macro
split uses only meals that have them and the insights say how many that is; unknown values are never counted as 0.

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
`Data/Recipes.txt` and `Data/Products.txt` are shipped. `My Pantry.txt`, `Receipts.txt`, `MealLog.txt` and `ShoppingList.txt` are created
on first use (the first run fills pantry and receipts with demo data). A clean rebuild removes them.


## Recipe Explorer

The Recipes screen is the single recipe workspace. It includes local recipe search, cuisine/time sorting, favorites, serving scaling, nutrition, pantry availability and shopping-list integration. Categories include Breakfast, Lunch, Dinner, Bread, Meal, Soup, Salad, Snack and Dessert.

The curated collection contains 250 recipes. The 50 additions (IDs 201–250) use only the supplied `Data/Products.txt` catalogue. Their per-serving calorie, protein, carbohydrate and fat values are calculated from the product nutrition fields. Milk/oil volume-to-mass assumptions are approximate and are disclosed in each recipe description.

Favorites and the 20 most recently viewed recipes are saved outside the installation directory under `%LOCALAPPDATA%\NutriTrack`. The “Cook with my pantry” filter uses the existing pantry and ingredient matching services. Recipe cards also show per-serving macro summaries when available.

## Full recipe-derived product catalogue

`NutriTrack.UI/Data/Products.txt` is the shared My Pantry/Recipes/Shopping/Receipts product catalogue. It contains 4,950 normalized ingredient entries extracted from the `ingredients` JSON field of the Hugging Face `datahiveai/recipes-with-nutrition` dataset (39,447 recipes; dataset URL: https://huggingface.co/datasets/datahiveai/recipes-with-nutrition). The 9,725 distinct ingredient strings in that source are covered by the catalogue's case-, accent-, punctuation- and plural-tolerant matcher.

The original 20 curated products retain their existing nutrition values. Other entries have blank per-100g nutrition because the source dataset contains recipe-level totals, not reliable per-ingredient nutrient profiles. NutriTrack treats blank values as unknown and will not use them to fabricate recipe nutrition calculations. Product categories are organizational classifications inferred from ingredient names, not source-provided dietary facts.


## Presentation
See `PRESENTATION_GUIDE.md` for a one-page summary of the architecture and a demo script.
