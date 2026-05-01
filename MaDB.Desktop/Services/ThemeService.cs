using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaDB.Desktop.Services;

public partial class ThemeService : ObservableObject, IThemeService
{
    [ObservableProperty]
    private ThemeVariant _currentTheme = ThemeVariant.Dark;

    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == ThemeVariant.Dark 
            ? ThemeVariant.Light 
            : ThemeVariant.Dark;
        
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = CurrentTheme;
        }
    }

    public void SetTheme(ThemeVariant theme)
    {
        CurrentTheme = theme;
        
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme;
        }
    }
}