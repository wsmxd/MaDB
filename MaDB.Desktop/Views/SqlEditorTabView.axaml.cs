using System.ComponentModel;
using Avalonia.Controls;
using MaDB.Desktop.ViewModels;

namespace MaDB.Desktop.Views;

public partial class SqlEditorTabView : UserControl
{
    private SqlEditorTabViewModel? _viewModel;

    public SqlEditorTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SetViewModel(DataContext as SqlEditorTabViewModel);
    }

    private void SetViewModel(SqlEditorTabViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is null)
        {
            QueryResultGridColumns.Rebuild(ResultGrid, []);
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlEditorTabViewModel.ResultColumnNames) && _viewModel is not null)
        {
            QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames);
        }
    }
}