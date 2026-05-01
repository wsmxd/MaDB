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