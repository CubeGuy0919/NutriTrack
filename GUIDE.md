# NutriTrack - presentation cheat sheet

## 1. The one-sentence pitch (memorize this)
"NutriTrack answers one question for a household: **what can I cook with what I have, and what do I still need to buy?**"

## 2. The whole app in 3 layers (draw this on the board)

```
  YOU CLICK        ->   NutriTrack.UI    (WPF screens, colours, images, data files)
  YOU CALCULATE    ->   NutriTrack.Core  (Models = data, Services = logic, NO WPF inside)
  YOU STORE        ->   files next to the .exe (Recipes.txt, Products.txt, My Pantry.txt, Receipts.txt, MealLog.txt, ShoppingList.txt)
```
Rule of thumb: **if you can click it, it is in UI. If it is a calculation or a file format, it is in Core.**
Why two projects? Core has no WPF, so the logic can be tested/reused without any window.

## 3. The 7 screens (one line each)
| Screen | Says | Core service behind it |
|---|---|---|
| Home (Home) | receipts this month, recipe of the day, "use soon" food | My PantryService, ReceiptService |
| Recipes | search, filter, servings slider, unit switch, pantry check | RecipeService, IngredientScaler, RecipeAvailabilityCalculator |
| **Menu (new)** | recommends breakfast, lunch, dinner + snack for a calorie target; "I ate this" logs the meal | MenuRecommender, MealLogService |
| Receipts | add / edit / delete shopping receipts | ReceiptService (Receipts.txt) |
| My Pantry | what is at home + expiry dates, "what can I cook?" | My PantryService (My Pantry.txt) |
| Shopping | missing ingredients; "Bought" moves them to the pantry | ShoppingListService (ShoppingList.txt) |
| **Statistics** | spending charts + NEW **Eating habits** (calories per day, meals by type, most eaten, cuisines, weekday, macros, insights) | StatisticsService, MealLogService |

## 4. The 5-step story of ONE recipe (the code tour)
1. **Load**: `RecipeService` reads `Recipes.txt`. Every line like "600 g ripe tomatoes" is turned into an `IngredientItem` (quantity 600, unit g, name) by `IngredientParser`.
2. **Scale**: the servings slider calls `IngredientScaler`: scale (x selected/base servings) -> convert units -> format text. *In this order.*
3. **Check pantry**: `RecipeAvailabilityCalculator` compares scaled ingredients with the pantry. `UnitConverter` first brings everything to g / ml / pieces, so 1 kg in the pantry covers 750 g.
4. **Shop**: "Add missing" -> `ShoppingListService`. "Bought" -> `My PantryService.AddOrIncrease`.
5. **Share state**: `AppServices` holds ONE shared pantry / receipts / shopping list so all screens agree.

## 5. Key classes - what each one is FOR
| Class | Job in 5 words |
|---|---|
| Recipe, IngredientItem | the recipe and its structured lines |
| Product | one food in the catalogue (nutrition per 100 g) |
| ProductMatcher | name -> product (ignores case, accents, plurals; never guesses) |
| IngredientParser | text line -> IngredientItem |
| IngredientScaler | recipe x servings -> display lines |
| Units.cs (UnitConverter, QuantityFormatter) | all unit maths and number text |
| RecipeAvailabilityCalculator | recipe vs pantry -> Available / Partial / Missing |
| My PantryService / ReceiptService / ShoppingListService | load, save, edit their data |
| StatisticsService | numbers for the charts |
| TextFile | safe saving (temp file, then swap, keeps .bak) |
| AppServices | one shared set of services |
| **MenuRecommender (new)** | scores recipes, picks one per meal slot |
| **MealLogService (new)** | saves what you ate (MealLog.txt) |
| **MenuMeal / MenuPlan (new)** | one recommended meal / the whole day |
| **MealLogEntry (new)** | one eaten meal + nutrition snapshot |

## 5b. The two new features in 30 seconds
- **Meal log**: the app only knew what you *buy*. Now "I ate this" (Recipes screen) saves what you *eat* -> `MealLog.txt` -> new Eating habits statistics.
- **Menu recommendation**: for each slot, score every recipe: 40 pantry match + 20 uses food that expires soon + 25 calorie fit + small bonuses
  (favorite, quick) and a variety rule (-25 if eaten in the last 7 days). Best score wins. Each card says *why*.
- Same architecture as before: logic in Core (no WPF), screens in UI, one shared `AppServices.MealLog`.

## 6. Likely teacher questions - short answers
- **Where is data stored?** Plain files next to the .exe; pantry/receipts as JSON, recipes/products as text. Demo data is created on the first run.
- **Why not hard-code units?** My Pantry maths always uses base units (g / ml / pieces); the Metric/Imperial switch only changes what is *displayed*.
- **What if an ingredient is unknown?** `ProductMatcher` is strict on purpose: it only *suggests* ("did you mean...") and the user decides.
- **What if saving fails?** Services keep `LastSaveError` and the screen shows it, nothing fails silently.
- **Why is the first menu always the same?** The best menu is deterministic (no randomness) so it is explainable; "Suggest another menu" picks one of the 5 best recipes per slot.
- **Why are some meals shown without protein / carbs / fat?** Only ~50 of 250 recipes have macro data; unknown is shown as unknown, never as 0.
- **Where does the eating data come from?** From `MealLog.txt`. The first run creates 3 weeks of demo meals; every "I ate this" click adds a real entry.
- **Why `AppServices`?** So every screen works on the same pantry/receipts/shopping list.

## 7. Live demo order (3-4 minutes)
1. Home screen -> 2. Recipes: open one, move the servings slider, switch Metric/Imperial -> 3. "Add missing ingredients" -> 4. Shopping: press "Bought" -> 5. My Pantry: item is there now -> 6. **Menu: show the recommended day, read one "Why this recipe?", change the diet, press "Suggest another menu", press "I ate this"** -> 7. **Statistics: scroll to Eating habits, the meal you just logged is in today's bar** -> 8. Receipts: show the spending charts.
