using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void OnTableContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Control control && control.ContextFlyout is not null)
        {
            control.ContextFlyout.ShowAt(control);
            e.Handled = true;
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
