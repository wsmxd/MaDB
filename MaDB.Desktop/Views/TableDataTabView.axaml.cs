using System.ComponentModel;
using Avalonia.Controls;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

public partial class TableDataTabView : UserControl
{
    private TableDataTabViewModel? _viewModel;

    public TableDataTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SetViewModel(DataContext as TableDataTabViewModel);
    }

    private void SetViewModel(TableDataTabViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is null)
        {
            QueryResultGridColumns.Rebuild(RowsGrid, [], true);
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        QueryResultGridColumns.Rebuild(RowsGrid, _viewModel.ColumnNames, false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableDataTabViewModel.ColumnNames) && _viewModel is not null)
        {
            QueryResultGridColumns.Rebuild(RowsGrid, _viewModel.ColumnNames, false);
        }
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (_viewModel is null || e.Row.DataContext is not QueryResultGridRow row)
        {
            return;
        }

        var columnName = e.Column.Header?.ToString();
        if (string.IsNullOrEmpty(columnName))
        {
            return;
        }

        if (e.EditingElement is TextBox textBox)
        {
            var newValue = textBox.Text ?? string.Empty;
            row[columnName] = newValue;
        }

        _viewModel.MarkAsChanged(row, columnName);
    }
}
