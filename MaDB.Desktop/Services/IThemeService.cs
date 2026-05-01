using Avalonia.Styling;

namespace MaDB.Desktop.Services;

public interface IThemeService
{
    ThemeVariant CurrentTheme { get; }
    
    void ToggleTheme();
    
    void SetTheme(ThemeVariant theme);
}