using System.Globalization;
using System.Windows.Data;

namespace WinSpaces.Converters;

/// <summary>
/// Convertit un booléen (IsHealthy) en icône textuelle pour le tableau de bord.
/// true → ✓, false → ⚠
/// </summary>
public sealed class BoolToStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isHealthy)
            return isHealthy ? "✓" : "⚠";
        
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
