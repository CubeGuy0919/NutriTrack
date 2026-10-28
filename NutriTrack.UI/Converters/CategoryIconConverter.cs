using System.Globalization;
using System.Windows.Data;

namespace NutriTrack.UI.Converters;

/// <summary>Category (Soup/Meal/Salad/Snack/Dessert) -> emoji icon for recipe cards.</summary>
public class CategoryIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string)?.ToLowerInvariant() switch
        {
            "soup" => "🍲",
            "meal" => "🍽️",
            "salad" => "🥗",
            "snack" => "🍿",
            "dessert" => "🍰",
            _ => "🍴"
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
