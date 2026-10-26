using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NutriTrack.Core.Models;

namespace NutriTrack.UI.Converters;

public class ExpiryBackgroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ExpiredBrush = new(Color.FromRgb(254, 226, 226)); 
    private static readonly SolidColorBrush ExpiringSoonBrush = new(Color.FromRgb(254, 243, 199)); 
    private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(234, 243, 235)); 
    private static readonly SolidColorBrush NoDateBrush = new(Color.FromRgb(243, 244, 246)); 

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExpiryStatus status)
        {
            return status switch
            {
                ExpiryStatus.Expired => ExpiredBrush,
                ExpiryStatus.ExpiringSoon => ExpiringSoonBrush,
                ExpiryStatus.Normal => NormalBrush,
                _ => NoDateBrush
            };
        }
        return NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw freshNotSupported();

    private static NotSupportedException freshNotSupported() => new();
}

public class ExpiryForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ExpiredBrush = new(Color.FromRgb(185, 28, 28)); 
    private static readonly SolidColorBrush ExpiringSoonBrush = new(Color.FromRgb(180, 83, 9)); 
    private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(46, 125, 50)); 
    private static readonly SolidColorBrush NoDateBrush = new(Color.FromRgb(107, 114, 128)); 

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExpiryStatus status)
        {
            return status switch
            {
                ExpiryStatus.Expired => ExpiredBrush,
                ExpiryStatus.ExpiringSoon => ExpiringSoonBrush,
                ExpiryStatus.Normal => NormalBrush,
                _ => NoDateBrush
            };
        }
        return NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}