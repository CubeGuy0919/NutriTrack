using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NutriTrack.UI.Converters;

/// <summary>"Easy" -> green, "Involved" -> amber badge.</summary>
public class DifficultyBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Easy = Freeze(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush Involved = Freeze(Color.FromRgb(0xC2, 0x7C, 0x1F));

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) == "Easy" ? Easy : Involved;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
