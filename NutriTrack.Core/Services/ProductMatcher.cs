using System.Globalization;
using System.Text;
using NutriTrack.Core.Models;

namespace NutriTrack.Core.Services;

/// <summary>
/// Resolves ingredient names to products from the EXISTING product list (Products.txt via ProductService).
/// It keeps no product data of its own.
///
/// Matching is deliberately strict: case-, whitespace- and accent-insensitive, plus simple singular/plural
/// on the last word ("tomato" = "tomatoes"). There is NO partial or fuzzy matching, so "chicken" will not
/// silently become "Chicken Breast" - <see cref="Suggest"/> offers such candidates, but the user must pick one.
/// </summary>
public class ProductMatcher
{
    private readonly IReadOnlyList<Product> _products;
    private readonly Dictionary<string, Product> _index = new();
    private readonly HashSet<string> _ambiguous = new();

    public ProductMatcher(IEnumerable<Product> products)
    {
        _products = products.ToList();

        foreach (var product in _products)
        {
            foreach (var key in KeysFor(product.Name))
            {
                if (_index.TryGetValue(key, out var existing) && existing.ProductId != product.ProductId)
                {
                    // two different products share a key: never guess between them
                    _ambiguous.Add(key);
                    continue;
                }
                _index[key] = product;
            }
        }
    }

    public IReadOnlyList<Product> Products => _products;

    /// <summary>Returns the product for an ingredient name, or null if it cannot be identified with confidence.</summary>
    public Product? Match(string? ingredientName)
    {
        if (string.IsNullOrWhiteSpace(ingredientName)) return null;

        foreach (var key in KeysFor(ingredientName))
        {
            if (_ambiguous.Contains(key)) continue;
            if (_index.TryGetValue(key, out var product)) return product;
        }
        return null;
    }

    public Product? GetById(int productId) => _products.FirstOrDefault(p => p.ProductId == productId);

    /// <summary>
    /// Candidate products whose name contains the typed text. For "did you mean" hints only -
    /// never used to accept an ingredient automatically.
    /// </summary>
    public IReadOnlyList<Product> Suggest(string? text, int max = 3)
    {
        string needle = Normalize(text);
        if (needle.Length < 2) return Array.Empty<Product>();

        return _products
            .Where(p => Normalize(p.Name).Contains(needle, StringComparison.Ordinal))
            .Take(max)
            .ToList();
    }

    // ---------------------------------------------------------------
    // Normalization
    // ---------------------------------------------------------------

    /// <summary>Lower-case, accent-free, single-spaced. "  Crème  Fraîche " becomes "creme fraiche".</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Very small, safe singularizer for the LAST word only: tomatoes->tomato, berries->berry, eggs->egg.</summary>
    public static string Singularize(string normalized)
    {
        if (normalized.Length < 4) return normalized;

        int lastSpace = normalized.LastIndexOf(' ');
        string head = lastSpace < 0 ? string.Empty : normalized.Substring(0, lastSpace + 1);
        string word = lastSpace < 0 ? normalized : normalized.Substring(lastSpace + 1);

        if (word.Length < 4) return normalized;

        if (word.EndsWith("ies", StringComparison.Ordinal))
            word = word.Substring(0, word.Length - 3) + "y";
        else if (word.EndsWith("oes", StringComparison.Ordinal))
            word = word.Substring(0, word.Length - 2);
        else if (word.EndsWith("ss", StringComparison.Ordinal) || word.EndsWith("us", StringComparison.Ordinal))
            return normalized; // "hummus", "asparagus", "molasses" stay as they are
        else if (word.EndsWith('s'))
            word = word.Substring(0, word.Length - 1);

        return head + word;
    }

    /// <summary>All normalized lookup keys for a name: full name, singular form, and the name without a "(...)" qualifier.</summary>
    private static IEnumerable<string> KeysFor(string name)
    {
        var keys = new List<string>();

        void Add(string raw)
        {
            string n = Normalize(raw);
            if (n.Length == 0) return;
            if (!keys.Contains(n)) keys.Add(n);
            string singular = Singularize(n);
            if (!keys.Contains(singular)) keys.Add(singular);
        }

        Add(name);

        int open = name.IndexOf('(');
        if (open > 0)
            Add(name.Substring(0, open)); // "Ground Beef (80/20)" also answers to "ground beef"

        return keys;
    }
}
