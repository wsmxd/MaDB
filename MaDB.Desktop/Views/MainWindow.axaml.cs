using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
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
            menuItem.DataContext is DatabaseTableViewModel table &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenTableTabCommand.Execute(table.Name);
        }
    }

    private void OnViewTableSchemaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem &&
            menuItem.DataContext is DatabaseTableViewModel table &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenTableSchemaCommand.Execute(table.Name);
        }
    }

    private void OnCopyTableNameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Clipboard API varies by Avalonia version
        // if (sender is MenuItem menuItem &&
        //     menuItem.DataContext is DatabaseTableViewModel table)
        // {
        //     var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        //     if (clipboard is not null)
        //     {
        //         _ = clipboard.SetTextAsync(table.Name);
        //     }
        // }
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