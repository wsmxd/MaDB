using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class QueryWorkspaceViewModel : ViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;

    public QueryWorkspaceViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;

        SqlText = "SELECT u.name, u.email, o.total, o.created_at\nFROM users u\nINNER JOIN orders o ON o.user_id = u.id\nORDER BY o.created_at DESC;";
        PreviewHint = _localizationService.GetLocalizedString("VmPreviewHint") ?? "Run the current SQL against the live SQLite database.";
        ResultCapSummary = _localizationService.GetLocalizedString("VmCapSummary") ?? "100 rows";
        ResultSummary = _localizationService.FormatLocalizedString("VmResultSummary", 0);
    }

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private string _previewHint = string.Empty;

    [ObservableProperty]
    private string _resultCapSummary = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private DataView? _queryResultRowsView;

    [RelayCommand]
    public async Task RunQueryPreviewAsync()
    {
        var queryText = string.IsNullOrWhiteSpace(SqlText)
            ? "SELECT u.name, u.email, o.total, o.created_at FROM users u INNER JOIN orders o ON o.user_id = u.id ORDER BY o.created_at DESC;"
            : SqlText;

        var result = await _workspaceService.ExecuteQueryAsync(queryText);
        QueryResultRowsView = CreateDataView(result);
        ResultSummary = _localizationService.FormatLocalizedString("VmResultSummary", result.Rows.Count);
    }

    public void RefreshLocalizedText()
    {
        PreviewHint = _localizationService.GetLocalizedString("VmPreviewHint") ?? "Run the current SQL against the live SQLite database.";
        ResultCapSummary = _localizationService.GetLocalizedString("VmCapSummary") ?? "100 rows";
    }

    private static DataView CreateDataView(QueryResult result)
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