using System.Globalization;
using System.Windows.Data;

namespace Mandal.Services;

/// <summary>Yazı boyutundan satır yüksekliği (×1.35).</summary>
public sealed class LineHeightConverter : IValueConverter
{
    public static LineHeightConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? d * 1.35 : 15.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
