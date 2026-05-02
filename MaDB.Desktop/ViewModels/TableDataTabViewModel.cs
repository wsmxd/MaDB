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
    private readonly HashSet<QueryResultGridRow> _modifiedRows = new();

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

    [RelayCommand]
    public async Task SaveChangesAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            
            foreach (var row in _modifiedRows)
            {
                var values = row.GetAllValues();
                await _workspaceService.UpdateTableRowAsync(_tableName, values);
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
