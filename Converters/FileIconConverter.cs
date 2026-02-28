using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileCatalog.Converters;

public class FileIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string ext = (value as string)?.ToLowerInvariant() ?? "";
        if (ext == "folder") return "📁";
        return ext switch
        {
            ".txt" or ".doc" or ".docx" or ".pdf" => "📄",
            ".jpg" or ".png" or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" => "🎵",
            ".mp4" or ".avi" or ".mkv" => "🎬",
            ".zip" or ".rar" or ".7z" => "📦",
            ".exe" or ".dll" or ".msi" => "⚙️",
            ".xls" or ".xlsx" or ".csv" => "📊",
            _ => "🗎" // Výchozí ikona pro neznámé soubory
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}