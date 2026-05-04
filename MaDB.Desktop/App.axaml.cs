using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MaDB.Desktop.Services;
using MaDB.Desktop.ViewModels;
using MaDB.Desktop.Views;

namespace MaDB.Desktop;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService<T>();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddDesktopServices();
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var connectionManager = _serviceProvider.GetRequiredService<ConnectionManagerService>();
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            desktop.ShutdownRequested += (_, _) => DisposeServiceProvider();

            base.OnFrameworkInitializationCompleted();
            await InitializeApplicationAsync(connectionManager, viewModel);
            return;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeApplicationAsync(
        ConnectionManagerService connectionManager,
        MainWindowViewModel viewModel)
    {
        await LoadConnectionManagerAsync(connectionManager);
        await InitializeViewModelAsync(viewModel);
    }

    private static async Task LoadConnectionManagerAsync(ConnectionManagerService connectionManager)
    {
        try
        {
            await connectionManager.LoadAsync();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"[App] Failed to load saved connections: {exception}");
        }
    }

    private static async Task InitializeViewModelAsync(MainWindowViewModel viewModel)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"[App] Failed to initialize main window: {exception}");
            viewModel.StatusMessage = $"{viewModel.LocalizationService.GetLocalizedString("VmStatusError") ?? "Database error:"} {exception.Message}";
            viewModel.ActivityFeed.AddActivity(viewModel.StatusMessage);
        }
    }

    private void DisposeServiceProvider()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serviceProvider = null;
    }
}
