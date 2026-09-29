using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NutriTrack.UI.Converters;

/// <summary>Category -> a distinct accent color, used for card top-bars and badges.</summary>
public class CategoryBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, Color> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Soup"] = Color.FromRgb(0xE0, 0x8E, 0x3E),
        ["Meal"] = Color.FromRgb(0x2E, 0x7D, 0x32),
        ["Salad"] = Color.FromRgb(0x5B, 0x9B, 0x4A),
        ["Snack"] = Color.FromRgb(0xC2, 0x62, 0x3A),
        ["Dessert"] = Color.FromRgb(0xB4, 0x4D, 0x8E),
    };

    private static readonly SolidColorBrush Fallback = new(Color.FromRgb(0x6B, 0x7A, 0x70));

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string ?? string.Empty;
        if (Map.TryGetValue(key, out var color))
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        return Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
