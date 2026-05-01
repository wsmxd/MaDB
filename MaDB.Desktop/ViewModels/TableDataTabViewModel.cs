using System;
using System.Data;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class TableDataTabViewModel : TabViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly string _tableName;

    public TableDataTabViewModel(
        string tableName,
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _tableName = tableName;
        _workspaceService = workspaceService;
        Title = tableName;
        Icon = "\u2630";
        Summary = localizationService.FormatLocalizedString("VmSelectedTableSummary", tableName, 0);
    }

    [ObservableProperty]
    private DataTable? _table;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            var result = await _workspaceService.ReadTableRowsAsync(_tableName);
            Table = ToDataTable(result);
            Summary = $"{_tableName} \u00b7 {result.Rows.Count} rows";
        }
        catch (Exception ex)
        {
            Table = null;
            ErrorMessage = ex.Message;
            Summary = $"{_tableName} \u00b7 error";
        }
    }

    private static DataTable ToDataTable(QueryResult result)
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

        return table;
    }
}