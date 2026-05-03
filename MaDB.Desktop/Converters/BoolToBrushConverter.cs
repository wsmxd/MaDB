using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MaDB.Desktop.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public string? TrueResourceKey { get; set; }
    public string? FalseResourceKey { get; set; }
    public string TrueColor { get; set; } = "#000000";
    public string FalseColor { get; set; } = "#000000";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        var key = isTrue ? TrueResourceKey : FalseResourceKey;
        
        if (key != null && Application.Current?.TryFindResource(key, out var resource) == true)
        {
            if (resource is Color color)
                return new SolidColorBrush(color);
            if (resource is IBrush brush)
                return brush;
        }
        
        var colorStr = isTrue ? TrueColor : FalseColor;
        return Brush.Parse(colorStr);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
