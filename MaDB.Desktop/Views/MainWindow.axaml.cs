using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using MaDB.Desktop.Services;
using MaDB.Desktop.ViewModels;
using MaDB.Desktop.Models;

namespace MaDB.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnSwitchDatabaseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var app = App.Current as App;
        var connectionManager = app?.GetService<ConnectionManagerService>();
        var localizationService = app?.GetService<ILocalizationService>();
        
        if (connectionManager is null || localizationService is null)
        {
            return;
        }

        var dialog = new ConnectDatabaseDialog(connectionManager, localizationService);
        var result = await dialog.ShowDialog<DatabaseConnectionInfo?>(this);
        
        if (dialog.ShouldConnect && result is not null)
        {
            await vm.SwitchDatabaseAsync(result);
        }
    }

    private void OnTableDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border &&
            border.DataContext is DatabaseTableViewModel table &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenTableTabCommand.Execute(table.Name);
        }
    }

    private async void OnCreateTableClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var dialog = new CreateTableDialog
        {
            DataContext = new CreateTableDialogViewModel(vm.LocalizationService)
        };

        var definition = await dialog.ShowDialog<TableDefinition?>(this);
        if (definition is not null)
        {
            await vm.CreateTableAsync(definition);
        }
    }

    private void OnViewTableDataClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem &&
            TryGetTable(menuItem) is { } table &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenTableTabCommand.Execute(table.Name);
        }
    }

    private void OnViewTableSchemaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem &&
            TryGetTable(menuItem) is { } table &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenTableSchemaCommand.Execute(table.Name);
        }
    }

    private async void OnDeleteTableClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem ||
            TryGetTable(menuItem) is not { } table ||
            DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var dialog = new DeleteTableConfirmDialog(table.Name);
        var confirmed = await dialog.ShowDialog<bool>(this);
        if (confirmed)
        {
            await vm.DeleteTableAsync(table.Name);
        }
    }

    private async void OnCopyTableNameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || TryGetTable(menuItem) is not { } table)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(table.Name);
        }
    }

    private static DatabaseTableViewModel? TryGetTable(MenuItem menuItem)
    {
        return menuItem.Tag as DatabaseTableViewModel
            ?? menuItem.DataContext as DatabaseTableViewModel;
    }

    private void OnTabTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border &&
            border.DataContext is TabViewModelBase tab &&
            DataContext is MainWindowViewModel vm)
        {
            vm.SelectedTab = tab;
        }
    }

    private void OnTabCloseTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock &&
            textBlock.DataContext is TabViewModelBase tab &&
            DataContext is MainWindowViewModel vm)
        {
            vm.CloseTabCommand.Execute(tab);
        }
    }
}
