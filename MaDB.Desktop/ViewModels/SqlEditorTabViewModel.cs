using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class SqlEditorTabViewModel : TabViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private readonly Action? _onSchemaChanged;
    private static int _counter;

    public SqlEditorTabViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService,
        Action? onSchemaChanged = null)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;
        _onSchemaChanged = onSchemaChanged;
        _counter++;
        Title = $"{localizationService.GetLocalizedString("TxtSqlEditorTitle") ?? "SQL Editor"} {_counter}";
        Icon = "\u270e";
        SqlText = "SELECT * FROM users;";
        StatusMessage = localizationService.GetLocalizedString("VmPreviewHint") ?? "Write SQL and click Execute.";
    }

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _resultColumnNames = [];

    [ObservableProperty]
    private ObservableCollection<QueryResultGridRow> _resultRows = [];

    [ObservableProperty]
    private bool _hasError;

    [RelayCommand]
    public async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(SqlText))
        {
            return;
        }

        try
        {
            HasError = false;
            StatusMessage = _localizationService.GetLocalizedString("VmStatusConnecting") ?? "Executing...";

            var trimmed = SqlText.Trim();
            var isQuery = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                          trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase) ||
                          trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

            if (isQuery)
            {
                var result = await _workspaceService.ExecuteQueryAsync(SqlText);
                var grid = QueryResultGrid.From(result);
                ResultColumnNames = grid.Columns;
                ResultRows = grid.Rows;
                ResultSummary = _localizationService.FormatLocalizedString("VmResultSummary", result.Rows.Count);
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
            }
            else
            {
                var affected = await _workspaceService.ExecuteNonQueryAsync(SqlText);
                ResultColumnNames = QueryResultGrid.Empty.Columns;
                ResultRows = QueryResultGrid.Empty.Rows;
                ResultSummary = $"{affected} rows affected.";
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
                _onSchemaChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
            ResultColumnNames = QueryResultGrid.Empty.Columns;
            ResultRows = QueryResultGrid.Empty.Rows;
            ResultSummary = string.Empty;
        }
    }
}