using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ZaraGON.Core.Enums;

namespace ZaraGON.UI.Converters;

public sealed class ServiceStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush RunningBrush = Freeze(Color.FromRgb(5, 150, 105));       // #059669 emerald-600
    private static readonly SolidColorBrush StartingBrush = Freeze(Color.FromRgb(217, 119, 6));      // #D97706 amber-600
    private static readonly SolidColorBrush StoppingBrush = Freeze(Color.FromRgb(234, 88, 12));      // #EA580C orange-600
    private static readonly SolidColorBrush ErrorBrush = Freeze(Color.FromRgb(220, 38, 38));          // #DC2626 red-600
    private static readonly SolidColorBrush DefaultBrush = Freeze(Color.FromRgb(156, 163, 175));      // #9CA3AF gray-400

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Running => RunningBrush,
                ServiceStatus.Starting => StartingBrush,
                ServiceStatus.Stopping => StoppingBrush,
                ServiceStatus.Error => ErrorBrush,
                ServiceStatus.NotInstalled => DefaultBrush,
                _ => DefaultBrush
            };
        }

        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
