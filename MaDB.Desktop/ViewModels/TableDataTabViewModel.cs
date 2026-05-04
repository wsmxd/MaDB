using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class TableDataTabViewModel : TabViewModelBase
{
    private const int PageSize = 100;

    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;
    private readonly string _tableName;
    private readonly HashSet<QueryResultGridRow> _modifiedRows = [];
    private readonly HashSet<QueryResultGridRow> _newRows = [];
    private readonly HashSet<QueryResultGridRow> _deletedRows = [];
    private List<QueryResultGridRow> _allRows = [];
    private int _lastTotalRowCount;
    private bool _hasLoadError;

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
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedRowCommand))]
    private ObservableCollection<QueryResultGridRow> _rows = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedRowCommand))]
    private QueryResultGridRow? _selectedRow;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _pageSummary = string.Empty;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _canGoPreviousPage;

    [ObservableProperty]
    private bool _canGoNextPage;

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
            _newRows.Clear();
            _deletedRows.Clear();
            HasUnsavedChanges = false;
            SelectedRow = null;
            var requestedPage = CurrentPage;

            var result = await _workspaceService.ReadTableRowsAsync(_tableName);
            var grid = QueryResultGrid.From(result);
            ColumnNames = grid.Columns;
            _allRows = grid.Rows.ToList();
            _lastTotalRowCount = result.Rows.Count;
            _hasLoadError = false;

            var rowsText = _localizationService.GetLocalizedString("VmRowsCount") ?? "rows";
            Summary = $"{_tableName} \u00b7 {result.Rows.Count} {rowsText}";
            ApplyPage(requestedPage);
        }
        catch (Exception ex)
        {
            ColumnNames = QueryResultGrid.Empty.Columns;
            Rows = QueryResultGrid.Empty.Rows;
            SelectedRow = null;
            _allRows = [];
            _lastTotalRowCount = 0;
            _hasLoadError = true;
            CurrentPage = 1;
            TotalPages = 1;
            CanGoPreviousPage = false;
            CanGoNextPage = false;
            PageSummary = _localizationService.FormatLocalizedString("VmPageSummary", 1, 1);
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

            foreach (var row in _deletedRows)
            {
                await _workspaceService.DeleteTableRowAsync(_tableName, row.GetAllValues(), cachedSchema);
            }

            foreach (var row in _modifiedRows.Where(row => !_newRows.Contains(row) && !_deletedRows.Contains(row)))
            {
                var values = row.GetAllValues();
                await _workspaceService.UpdateTableRowAsync(_tableName, values, cachedSchema);
            }

            foreach (var row in _newRows)
            {
                await _workspaceService.InsertTableRowAsync(_tableName, row.GetAllValues(), cachedSchema);
            }

            _modifiedRows.Clear();
            _newRows.Clear();
            _deletedRows.Clear();
            HasUnsavedChanges = false;

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public void RefreshLocalizedText()
    {
        var rowsText = _localizationService.GetLocalizedString("VmRowsCount") ?? "rows";
        var errorText = _localizationService.GetLocalizedString("VmError") ?? "error";

        Summary = _hasLoadError
            ? $"{_tableName} \u00b7 {errorText}"
            : $"{_tableName} \u00b7 {_lastTotalRowCount} {rowsText}";
        PageSummary = _localizationService.FormatLocalizedString("VmPageSummary", CurrentPage, TotalPages);
    }

    [RelayCommand(CanExecute = nameof(CanAddRow))]
    private void AddRow()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columnName in ColumnNames)
        {
            values[columnName] = string.Empty;
        }

        var newRow = new QueryResultGridRow(values);
        _newRows.Add(newRow);
        _allRows.Add(newRow);
        _lastTotalRowCount = _allRows.Count;

        ApplyPage(int.MaxValue, newRow);
        _modifiedRows.Remove(newRow);
        _deletedRows.Remove(newRow);
        HasUnsavedChanges = true;
        RefreshLocalizedText();
        AddRowCommand.NotifyCanExecuteChanged();
        DeleteSelectedRowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedRow))]
    private void DeleteSelectedRow()
    {
        if (SelectedRow is null)
        {
            return;
        }

        var deletedRow = SelectedRow;
        var deletedIndex = _allRows.IndexOf(deletedRow);

        if (deletedIndex < 0)
        {
            SelectedRow = null;
            return;
        }

        _allRows.RemoveAt(deletedIndex);
        _modifiedRows.Remove(deletedRow);

        if (!_newRows.Remove(deletedRow))
        {
            _deletedRows.Add(deletedRow);
        }

        _lastTotalRowCount = _allRows.Count;

        var nextSelectionIndex = Math.Min(deletedIndex, _allRows.Count - 1);
        var nextSelection = nextSelectionIndex >= 0 ? _allRows[nextSelectionIndex] : null;

        ApplyPage(CurrentPage, nextSelection);
        HasUnsavedChanges = _modifiedRows.Count > 0 || _newRows.Count > 0 || _deletedRows.Count > 0;
        RefreshLocalizedText();
        AddRowCommand.NotifyCanExecuteChanged();
        DeleteSelectedRowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        ApplyPage(CurrentPage - 1);
    }

    [RelayCommand]
    private void NextPage()
    {
        ApplyPage(CurrentPage + 1);
    }

    private bool CanAddRow()
    {
        return _workspaceService.AccessMode != DatabaseAccessMode.ReadOnly && ColumnNames.Count > 0;
    }

    private bool CanDeleteSelectedRow()
    {
        return _workspaceService.AccessMode != DatabaseAccessMode.ReadOnly && SelectedRow is not null;
    }

    private void ApplyPage(int requestedPage, QueryResultGridRow? selectedRow = null)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_allRows.Count / (double)PageSize));
        var currentPage = Math.Clamp(requestedPage, 1, totalPages);

        CurrentPage = currentPage;
        TotalPages = totalPages;
        Rows = new ObservableCollection<QueryResultGridRow>(
            _allRows
                .Skip((currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList());

        SelectedRow = selectedRow is not null && Rows.Contains(selectedRow) ? selectedRow : null;

        PageSummary = _localizationService.FormatLocalizedString("VmPageSummary", currentPage, totalPages);
        CanGoPreviousPage = currentPage > 1;
        CanGoNextPage = currentPage < totalPages;
    }

    partial void OnColumnNamesChanged(IReadOnlyList<string> value)
    {
        AddRowCommand.NotifyCanExecuteChanged();
    }
}
