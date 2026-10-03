using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinSpaces.Converters;

/// <summary>
/// Vrai si la valeur (ToString) est égale au paramètre. En retour (RadioButton
/// coché), renvoie le paramètre converti vers le type cible (enum ou string).
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return System.Windows.Data.Binding.DoNothing;
        var text = parameter.ToString()!;
        return targetType.IsEnum ? Enum.Parse(targetType, text) : text;
    }
}

/// <summary>Visible si la valeur (ToString) est égale au paramètre.</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible si la valeur est vraie (ou fausse avec le paramètre « Inverse »).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        if (string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
