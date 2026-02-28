using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileCatalog.Converters;

public class TicksToDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 1. Ochrana proti null u složek a systémovému UnsetValue při startu UI
        if (value == null || value == AvaloniaProperty.UnsetValue)
            return string.Empty;

        long ticks;
        try
        {
            // 2. Extrémně bezpečný a robustní převod na číslo
            ticks = System.Convert.ToInt64(value);
        }
        catch
        {
            return string.Empty;
        }

        if (ticks == 0) return string.Empty;

        var dt = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
        return dt.ToString("g", CultureInfo.CurrentCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}