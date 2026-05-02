using System;
using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using MaDB.Desktop.ViewModels;
using TextMateSharp.Grammars;

namespace MaDB.Desktop.Views;

public partial class SqlEditorTabView : UserControl
{
    private SqlEditorTabViewModel? _viewModel;
    private bool _isSyncingText;
    private TextMate.Installation? _textMateInstallation;

    public SqlEditorTabView()
    {
        InitializeComponent();
        InitializeSqlEditor();
        DataContextChanged += (_, _) => SetViewModel(DataContext as SqlEditorTabViewModel);
    }

    private void InitializeSqlEditor()
    {
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = SqlTextEditor.InstallTextMate(registryOptions);
        _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId("sql"));
        SqlTextEditor.TextChanged += OnSqlEditorTextChanged;
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
        SetEditorText(_viewModel.SqlText);
        QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlEditorTabViewModel.ResultColumnNames) && _viewModel is not null)
        {
            QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames);
        }

        if (e.PropertyName == nameof(SqlEditorTabViewModel.SqlText) && _viewModel is not null)
        {
            SetEditorText(_viewModel.SqlText);
        }
    }

    private void OnSqlEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isSyncingText || _viewModel is null)
        {
            return;
        }

        _isSyncingText = true;
        _viewModel.SqlText = SqlTextEditor.Text;
        _isSyncingText = false;
    }

    private void SetEditorText(string text)
    {
        if (_isSyncingText || SqlTextEditor.Text == text)
        {
            return;
        }

        _isSyncingText = true;
        SqlTextEditor.Text = text;
        _isSyncingText = false;
    }
}
