using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SolarSystem3D.App.ViewModels;

/// <summary>Maps a slider position (log10) to a multiplier and back, e.g. 0..3 ↔ 1..1000.</summary>
public sealed class Log10Converter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Math.Log10(Math.Max(1e-9, System.Convert.ToDouble(value, culture)));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Math.Pow(10, System.Convert.ToDouble(value, culture));
}

/// <summary>Collapses an element when the bound string is empty.</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Turns a colour into a brush (for the legend).</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Color c ? new SolidColorBrush(c) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
