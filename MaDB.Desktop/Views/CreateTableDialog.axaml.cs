using Avalonia.Controls;
using Avalonia.Interactivity;
using MaDB.Desktop.Models;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

public partial class CreateTableDialog : Window
{
    public CreateTableDialog()
    {
        InitializeComponent();
        Opened += (_, _) => TableNameBox.Focus();
    }

    private CreateTableDialogViewModel? ViewModel => DataContext as CreateTableDialogViewModel;

    private void OnAddColumnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.AddColumn();
    }

    private void OnRemoveColumnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TableColumnEditorViewModel column)
        {
            ViewModel?.RemoveColumn(column);
        }
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.TryBuildDefinition(out var definition, out _))
        {
            Close(definition);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}