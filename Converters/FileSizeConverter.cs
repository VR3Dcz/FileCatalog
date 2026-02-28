using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileCatalog.Converters;

public class FileSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 1. Ochrana proti null u složek a systémovému UnsetValue při startu UI
        if (value == null || value == AvaloniaProperty.UnsetValue)
            return string.Empty;

        long bytes;
        try
        {
            // 2. Extrémně bezpečný a robustní převod na číslo
            bytes = System.Convert.ToInt64(value);
        }
        catch
        {
            return string.Empty;
        }

        if (bytes == 0) return "0 B";

        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int counter = 0;
        decimal number = (decimal)bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return string.Format(culture, "{0:n1} {1}", number, suffixes[counter]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}