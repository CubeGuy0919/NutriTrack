# NutriTrack implementation status — Recipe Explorer revision

## Completed
- Removed the Discover navigation entry and its dedicated WPF screen.
- Removed the Discover-only SQLite dataset service, model and Microsoft.Data.Sqlite dependency; no recipe dataset database is initialized or required.
- Moved Favorites into Recipes as a persistent category filter and recipe-detail toggle. Favorites are stored under LocalAppData, outside the install directory.
- Added Breakfast, Lunch, Dinner and Bread categories while retaining existing categories.
- Added a “Cook with my pantry” filter using existing ingredient availability and pantry services.
- Added 50 original recipes (IDs 201–250) using only products in `Data/Products.txt`; their per-serving nutrition is calculated from the supplied per-100 product values.
- Preserved existing serving scaling, nutrition display, ingredient availability, shopping-list generation, recipe CRUD, pantry, receipts and shopping workflows.
- Updated the Recipes screen header and category presentation; kept existing card/detail visual language.

## Validation
- `Recipes.txt` contains 250 curated recipes (200 existing + 50 added).
- New recipes use catalogue product names and metric units; nutrition fields are populated from the product catalogue.
- XAML and project XML parsed successfully after edits.
- .NET build and Windows WPF runtime tests were not run because the .NET SDK is unavailable in this environment.

## Nutrition note
Nutrition for the 50 additions is derived from the supplied product catalogue. Volume ingredients use the catalogue's ml basis; conversion to gram-equivalent nutrient calculations is approximate where density is not 1 g/ml. Values are estimates, not laboratory measurements.

## Product catalogue expansion (v15)
- Rebuilt `NutriTrack.UI/Data/Products.txt` from every `ingredients[].food` value in the supplied `recipes-with-nutrition.csv` (39,447 recipe rows).
- Catalogue now contains 4,950 distinct selectable products, covering all 9,725 distinct source ingredient strings after the same case/accent/plural/quote normalization used by the product matcher.
- Preserved the original 20 curated product IDs and their existing per-100 nutrition values.
- Added the remaining ingredient products with inferred organizational categories and blank nutrition fields. The CSV provides recipe-level nutrition totals, not per-ingredient nutrient profiles; unknown product nutrition is deliberately not fabricated.
- Product loading now streams the TSV catalogue. `Product` nutrition fields are nullable; recipe nutrition calculation refuses to calculate when a required ingredient lacks verified nutrition.
- Pantry product selector uses case-insensitive text search and now exposes the full catalogue.
- Source attribution: `datahiveai/recipes-with-nutrition` dataset on Hugging Face; ingredient names extracted from the `ingredients` JSON column. The raw 429 MB CSV is not duplicated in the delivery archive.
- Validation: 39,447 CSV rows read; 9,725 distinct ingredient strings; 4,950 catalogue entries; all source ingredient strings resolve via ProductMatcher-compatible normalization; 4,950 unique IDs; no embedded tabs/newlines in product names.
- .NET build/runtime verification remains pending because `dotnet` is not installed in this environment.
