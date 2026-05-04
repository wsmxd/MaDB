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
    private const int MaxPreviewRows = 100;

    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<Task>? _onSchemaChanged;
    private static int _counter;

    public SqlEditorTabViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService,
        Func<Task>? onSchemaChanged = null)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;
        _onSchemaChanged = onSchemaChanged;
        _counter++;
        Title = $"{localizationService.GetLocalizedString("TxtSqlEditorTitle") ?? "SQL Editor"} {_counter}";
        Icon = "\u270e";
        StatusMessage = localizationService.GetLocalizedString("VmPreviewHint") ?? "Write SQL and click Execute.";
    }

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedRowCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPreviewRowCommand))]
    private IReadOnlyList<string> _resultColumnNames = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedRowCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddPreviewRowCommand))]
    private ObservableCollection<QueryResultGridRow> _resultRows = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedRowCommand))]
    private QueryResultGridRow? _selectedResultRow;

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
                var previewRowCount = Math.Min(grid.Rows.Count, MaxPreviewRows);
                var previewRows = new List<QueryResultGridRow>(previewRowCount);

                for (var index = 0; index < previewRowCount; index++)
                {
                    previewRows.Add(grid.Rows[index]);
                }

                ResultRows = new ObservableCollection<QueryResultGridRow>(previewRows);
                SelectedResultRow = null;

                ResultSummary = result.Rows.Count > MaxPreviewRows
                    ? _localizationService.FormatLocalizedString("VmResultSummaryLimited", result.Rows.Count, MaxPreviewRows)
                    : _localizationService.FormatLocalizedString("VmResultSummary", result.Rows.Count);
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
            }
            else
            {
                var affected = await _workspaceService.ExecuteNonQueryAsync(SqlText);
                ResultColumnNames = QueryResultGrid.Empty.Columns;
                ResultRows = QueryResultGrid.Empty.Rows;
                SelectedResultRow = null;
                ResultSummary = _localizationService.FormatLocalizedString("VmRowsAffected", affected);
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
                if (_onSchemaChanged is not null)
                {
                    await _onSchemaChanged();
                }
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
            ResultColumnNames = QueryResultGrid.Empty.Columns;
            ResultRows = QueryResultGrid.Empty.Rows;
            SelectedResultRow = null;
            ResultSummary = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddPreviewRow))]
    private void AddPreviewRow()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columnName in ResultColumnNames)
        {
            values[columnName] = string.Empty;
        }

        var newRow = new QueryResultGridRow(values);
        ResultRows.Add(newRow);
        SelectedResultRow = newRow;
        AddPreviewRowCommand.NotifyCanExecuteChanged();
        DeleteSelectedRowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedRow))]
    private void DeleteSelectedRow()
    {
        if (SelectedResultRow is null)
        {
            return;
        }

        var selectedIndex = ResultRows.IndexOf(SelectedResultRow);
        if (selectedIndex < 0)
        {
            SelectedResultRow = null;
            return;
        }

        ResultRows.RemoveAt(selectedIndex);

        if (ResultRows.Count == 0)
        {
            SelectedResultRow = null;
            AddPreviewRowCommand.NotifyCanExecuteChanged();
            DeleteSelectedRowCommand.NotifyCanExecuteChanged();
            return;
        }

        var nextIndex = Math.Min(selectedIndex, ResultRows.Count - 1);
        SelectedResultRow = ResultRows[nextIndex];
        AddPreviewRowCommand.NotifyCanExecuteChanged();
        DeleteSelectedRowCommand.NotifyCanExecuteChanged();
    }

    private bool CanAddPreviewRow()
    {
        return ResultColumnNames.Count > 0 && ResultRows.Count < MaxPreviewRows;
    }

    private bool CanDeleteSelectedRow()
    {
        return SelectedResultRow is not null;
    }
}