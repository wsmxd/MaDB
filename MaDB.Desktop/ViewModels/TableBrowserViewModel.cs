using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class TableBrowserViewModel : ViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private int _lastSchemaObjectCount;
    private bool _suppressSelectedTableLoad;

    public TableBrowserViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;

        SchemaSummary = _localizationService.FormatLocalizedString("VmSchemaSummary", 0);
        SelectedTableSummary = _localizationService.GetLocalizedString("VmSelectedTableEmpty") ?? "Select a table to view its rows.";
    }

    [ObservableProperty]
    private string _schemaSummary = string.Empty;

    [ObservableProperty]
    private string _selectedTableSummary = string.Empty;

    [ObservableProperty]
    private DatabaseTableViewModel? _selectedTable;

    [ObservableProperty]
    private DataView? _selectedTableRowsView;

    public ObservableCollection<DatabaseTableViewModel> Tables { get; } = [];

    [RelayCommand]
    public async Task LoadSchemaAsync(string? preferredTableName = null)
    {
        var schema = await _workspaceService.ReadSchemaAsync();
        _lastSchemaObjectCount = schema.Tables.Count;

        var previousSelectedName = SelectedTable?.Name;
        ReplaceItems(Tables, schema.Tables.Select(ToTableItem));

        var nextSelected = Tables.FirstOrDefault(table => table.Name == preferredTableName)
            ?? Tables.FirstOrDefault(table => table.Name == previousSelectedName)
            ?? Tables.FirstOrDefault();

        SetSelectedTableSilently(nextSelected);
        await LoadSelectedTableAsync(nextSelected);
        UpdateSchemaSummary();
    }

    partial void OnSelectedTableChanged(DatabaseTableViewModel? value)
    {
        if (_suppressSelectedTableLoad)
        {
            return;
        }

        _ = LoadSelectedTableAsync(value);
    }

    private async Task LoadSelectedTableAsync(DatabaseTableViewModel? table)
    {
        if (table is null)
        {
            SelectedTableRowsView = null;
            SelectedTableSummary = _localizationService.GetLocalizedString("VmSelectedTableEmpty") ?? "Select a table to view its rows.";
            return;
        }

        var result = await _workspaceService.ReadTableRowsAsync(table.Name);
        SelectedTableRowsView = CreateDataView(result);
        SelectedTableSummary = _localizationService.FormatLocalizedString("VmSelectedTableSummary", table.Name, result.Rows.Count);
    }

    public void RefreshLocalizedText()
    {
        SchemaSummary = _localizationService.FormatLocalizedString("VmSchemaSummary", _lastSchemaObjectCount);
        if (SelectedTable is not null)
        {
            SelectedTableSummary = _localizationService.FormatLocalizedString("VmSelectedTableSummary", SelectedTable.Name, SelectedTableRowsView?.Count ?? 0);
        }

        foreach (var table in Tables)
        {
            table.RefreshLocalizedText();
        }
    }

    private void UpdateSchemaSummary()
    {
        SchemaSummary = _localizationService.FormatLocalizedString("VmSchemaSummary", _lastSchemaObjectCount);
    }

    private void SetSelectedTableSilently(DatabaseTableViewModel? table)
    {
        _suppressSelectedTableLoad = true;
        SelectedTable = table;
        _suppressSelectedTableLoad = false;
    }

    private DatabaseTableViewModel ToTableItem(TableSchema table)
    {
        return new DatabaseTableViewModel(
            table.Name,
            table.Type.ToString(),
            table.Columns.Count,
            table.DefinitionSql,
            _localizationService);
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

    private static void ReplaceItems<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}