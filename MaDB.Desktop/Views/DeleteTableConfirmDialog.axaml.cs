using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaDB.Desktop.Views;

public partial class DeleteTableConfirmDialog : Window
{
    public DeleteTableConfirmDialog()
        : this(string.Empty)
    {
    }

    public DeleteTableConfirmDialog(string tableName)
    {
        InitializeComponent();
        TableNameText.Text = tableName;
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}