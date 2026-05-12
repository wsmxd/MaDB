using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class TableDataTabViewModel : TabViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private readonly string _tableName;
    private readonly HashSet<QueryResultGridRow> _modifiedRows = [];

    public TableDataTabViewModel(
        string tableName,
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _tableName = tableName;
        _workspaceService = workspaceService;
        _localizationService = localizationService;
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

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public void MarkAsChanged(QueryResultGridRow row, string columnName)
    {
        _modifiedRows.Add(row);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            _modifiedRows.Clear();
            HasUnsavedChanges = false;
            
            var result = await _workspaceService.ReadTableRowsAsync(_tableName);
            var grid = QueryResultGrid.From(result);
            ColumnNames = grid.Columns;
            Rows = grid.Rows;
            
            var rowsText = _localizationService.GetLocalizedString("VmRowsCount") ?? "rows";
            Summary = $"{_tableName} \u00b7 {result.Rows.Count} {rowsText}";
        }
        catch (Exception ex)
        {
            ColumnNames = QueryResultGrid.Empty.Columns;
            Rows = QueryResultGrid.Empty.Rows;
            ErrorMessage = ex.Message;
            var errorText = _localizationService.GetLocalizedString("VmError") ?? "error";
            Summary = $"{_tableName} \u00b7 {errorText}";
        }
    }

    [RelayCommand]
    public async Task SaveChangesAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            
            var cachedSchema = await _workspaceService.ReadSchemaAsync();
            
            foreach (var row in _modifiedRows)
            {
                var values = row.GetAllValues();
                await _workspaceService.UpdateTableRowAsync(_tableName, values, cachedSchema);
            }
            
            _modifiedRows.Clear();
            HasUnsavedChanges = false;
            
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
