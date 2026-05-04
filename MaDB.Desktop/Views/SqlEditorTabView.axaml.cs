using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit.TextMate;
using MaDB.Desktop.ViewModels;
using TextMateSharp.Grammars;

namespace MaDB.Desktop.Views;

public partial class SqlEditorTabView : UserControl, IDisposable
{
    private SqlEditorTabViewModel? _viewModel;
    private bool _isSyncingText;
    private TextMate.Installation? _textMateInstallation;
    private bool _disposed;

    public SqlEditorTabView()
    {
        InitializeComponent();
        InitializeSqlEditor();
        DataContextChanged += (_, _) => SetViewModel(DataContext as SqlEditorTabViewModel);
        SetViewModel(DataContext as SqlEditorTabViewModel);
    }

    private void InitializeSqlEditor()
    {
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = SqlTextEditor.InstallTextMate(registryOptions);
        _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId("sql"));
        SqlTextEditor.TextChanged += OnSqlEditorTextChanged;
        SqlTextEditor.PointerWheelChanged += OnSqlEditorPointerWheelChanged;
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
            QueryResultGridColumns.Rebuild(ResultGrid, [], false);
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SetEditorText(_viewModel.SqlText);
        QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames, false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlEditorTabViewModel.ResultColumnNames) && _viewModel is not null)
        {
            QueryResultGridColumns.Rebuild(ResultGrid, _viewModel.ResultColumnNames, false);
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

    private void OnSqlEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var delta = e.Delta.Y;
            var currentSize = SqlTextEditor.FontSize;
            var newSize = currentSize + (delta > 0 ? 1 : -1);
            
            if (newSize >= 8 && newSize <= 32)
            {
                SqlTextEditor.FontSize = newSize;
            }
            
            e.Handled = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                SqlTextEditor.TextChanged -= OnSqlEditorTextChanged;
                SqlTextEditor.PointerWheelChanged -= OnSqlEditorPointerWheelChanged;
                _textMateInstallation?.Dispose();
                _textMateInstallation = null;
                
                if (_viewModel is not null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }
            }
            _disposed = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
