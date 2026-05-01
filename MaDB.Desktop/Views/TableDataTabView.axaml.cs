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
            QueryResultGridColumns.Rebuild(RowsGrid, []);
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        QueryResultGridColumns.Rebuild(RowsGrid, _viewModel.ColumnNames);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableDataTabViewModel.ColumnNames) && _viewModel is not null)
        {
            QueryResultGridColumns.Rebuild(RowsGrid, _viewModel.ColumnNames);
        }
    }
}
