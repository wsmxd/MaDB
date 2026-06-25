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
        var dbDir = GetLocalDataDir("Databases");
        Directory.CreateDirectory(dbDir);
        var defaultDbPath = Path.Combine(dbDir, "default.sqlite");
        services.AddSingleton(provider => new DatabaseWorkspaceService(defaultDbPath, DatabaseDialect.Sqlite));
        
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionManagerViewModel>();
        services.AddTransient<TableBrowserViewModel>();
        services.AddTransient<ActivityFeedViewModel>();
        
        return services;
    }

    private static string GetLocalDataDir(string subPath)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.GetTempPath();
        }

        return Path.Combine(baseDir, "MaDB", "Desktop", subPath);
    }
}