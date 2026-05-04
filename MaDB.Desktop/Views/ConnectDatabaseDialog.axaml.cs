using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaDB.Core;
using MaDB.Desktop.Models;
using MaDB.Desktop.Services;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

public partial class ConnectDatabaseDialog : Window
{
    private readonly ConnectionDialogViewModel _viewModel;

    public ConnectDatabaseDialog(
        ConnectionManagerService connectionManager,
        ILocalizationService localizationService)
    {
        InitializeComponent();
        _viewModel = new ConnectionDialogViewModel(connectionManager, localizationService);
        DataContext = _viewModel;
    }

    public DatabaseConnectionInfo? Result { get; private set; }
    public bool ShouldConnect { get; private set; }

    private void OnSavedConnectionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is DatabaseConnectionInfo connection)
        {
            _viewModel.EditConnectionCommand.Execute(connection);
        }
    }

    private void OnNewConnectionClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.NewConnectionCommand.Execute(null);
    }

    private async void OnBrowseFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SQLite Database",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SQLite Database") { Patterns = ["*.sqlite", "*.db", "*.sqlite3"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ]
        });

        if (files.Count > 0)
        {
            _viewModel.DatabasePath = files[0].Path.LocalPath;
        }
    }

    private async void OnDeleteConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SavedConnections.Count <= 1) return;
        
        await _viewModel.RemoveConnectionCommand.ExecuteAsync(null);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        ShouldConnect = false;
        Close(null);
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.Validate()) return;
        
        await _viewModel.SaveConnectionAsync();
        Result = _viewModel.BuildConnectionInfo();
        ShouldConnect = true;
        Close(Result);
    }
}
