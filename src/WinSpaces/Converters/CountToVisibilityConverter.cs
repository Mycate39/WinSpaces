using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinSpaces.Converters;

/// <summary>
/// Convertit un count > 0 en Visibility.Visible, sinon Visibility.Collapsed.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is int count && count > 0;
        // Paramètre « Inverse » : visible quand la liste est vide (état vide).
        if (string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase))
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
