using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private IReadOnlyList<string> _columnNames = [];

    [ObservableProperty]
    private ObservableCollection<QueryResultGridRow> _rows = [];

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
            var grid = QueryResultGrid.From(result);
            ColumnNames = grid.Columns;
            Rows = grid.Rows;
            Summary = $"{_tableName} \u00b7 {result.Rows.Count} rows";
        }
        catch (Exception ex)
        {
            ColumnNames = QueryResultGrid.Empty.Columns;
            Rows = QueryResultGrid.Empty.Rows;
            ErrorMessage = ex.Message;
            Summary = $"{_tableName} \u00b7 error";
        }
    }
}
