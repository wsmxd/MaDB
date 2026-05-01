using System;
using System.Data;
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
    private static int _counter;

    public SqlEditorTabViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;
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
    private DataView? _resultTable;

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
                ResultTable = ToDataView(result);
                ResultSummary = _localizationService.FormatLocalizedString("VmResultSummary", result.Rows.Count);
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
            }
            else
            {
                var affected = await _workspaceService.ExecuteNonQueryAsync(SqlText);
                ResultTable = null;
                ResultSummary = $"{affected} rows affected.";
                StatusMessage = _localizationService.GetLocalizedString("VmStatusReady") ?? "Done.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
            ResultTable = null;
            ResultSummary = string.Empty;
        }
    }

    private static DataView ToDataView(QueryResult result)
    {
        var table = new DataTable();
        foreach (var columnName in result.Columns)
        {
            table.Columns.Add(columnName, typeof(string));
        }

        foreach (var row in result.Rows)
        {
            var dataRow = table.NewRow();
            foreach (var columnName in result.Columns)
            {
                row.TryGetValue(columnName, out var value);
                dataRow[columnName] = value?.ToString() ?? string.Empty;
            }
            table.Rows.Add(dataRow);
        }

        return table.DefaultView;
    }
}
