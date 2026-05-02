using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MaDB.Desktop.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public string TrueColor { get; set; } = "#000000";
    public string FalseColor { get; set; } = "#000000";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is true ? TrueColor : FalseColor;
        return Brush.Parse(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
