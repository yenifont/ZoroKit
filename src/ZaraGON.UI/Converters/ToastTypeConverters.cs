using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.Converters;

public sealed class ToastTypeToBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = Freeze(Color.FromRgb(0x0F, 0x76, 0x2D));   // deep green
    private static readonly SolidColorBrush ErrorBrush = Freeze(Color.FromRgb(0xB9, 0x1C, 0x1C));     // deep red
    private static readonly SolidColorBrush WarningBrush = Freeze(Color.FromRgb(0x92, 0x60, 0x0A));    // deep amber
    private static readonly SolidColorBrush InfoBrush = Freeze(Color.FromRgb(0x0C, 0x5E, 0xB8));       // deep blue
    private static readonly SolidColorBrush DefaultBrush = Freeze(Color.FromRgb(0x1A, 0x1D, 0x26));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ToastType type
            ? type switch
            {
                ToastType.Success => SuccessBrush,
                ToastType.Error => ErrorBrush,
                ToastType.Warning => WarningBrush,
                ToastType.Info => InfoBrush,
                _ => DefaultBrush
            }
            : DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ToastTypeToIconForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = Freeze(Color.FromRgb(0x4A, 0xDE, 0x80));   // bright green
    private static readonly SolidColorBrush ErrorBrush = Freeze(Color.FromRgb(0xFC, 0xA5, 0xA5));     // light red
    private static readonly SolidColorBrush WarningBrush = Freeze(Color.FromRgb(0xFB, 0xBF, 0x24));    // yellow
    private static readonly SolidColorBrush InfoBrush = Freeze(Color.FromRgb(0x7D, 0xD3, 0xFC));       // light blue
    private static readonly SolidColorBrush DefaultBrush = Freeze(Colors.White);

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ToastType type
            ? type switch
            {
                ToastType.Success => SuccessBrush,
                ToastType.Error => ErrorBrush,
                ToastType.Warning => WarningBrush,
                ToastType.Info => InfoBrush,
                _ => DefaultBrush
            }
            : DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
