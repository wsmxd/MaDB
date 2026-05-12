using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
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
        
        // Database workspace
        var workspacePath = Path.Combine(AppContext.BaseDirectory, "ma-desktop-demo.sqlite");
        services.AddSingleton(provider => new DatabaseWorkspaceService(workspacePath));
        
        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionManagerViewModel>();
        services.AddTransient<TableBrowserViewModel>();
        services.AddTransient<ActivityFeedViewModel>();
        
        return services;
    }
}