using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using MaDB.Core;
using MaDB.Desktop.Services;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ConnectionManagerService>();
        
        // Database workspace - default SQLite
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MaDB",
            "Desktop",
            "Databases");
        Directory.CreateDirectory(appDataDir);
        var defaultDbPath = Path.Combine(appDataDir, "default.sqlite");
        services.AddSingleton(provider => new DatabaseWorkspaceService(defaultDbPath, DatabaseDialect.Sqlite));
        
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionManagerViewModel>();
        services.AddTransient<TableBrowserViewModel>();
        services.AddTransient<ActivityFeedViewModel>();
        
        return services;
    }
}