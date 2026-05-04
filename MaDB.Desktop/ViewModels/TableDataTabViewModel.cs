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
    private ObservableCollection<QueryResultGridRow> _rows = [];

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
            HasUnsavedChanges = false;
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

    public void RefreshLocalizedText()
    {
        var rowsText = _localizationService.GetLocalizedString("VmRowsCount") ?? "rows";
        var errorText = _localizationService.GetLocalizedString("VmError") ?? "error";

        Summary = _hasLoadError
            ? $"{_tableName} \u00b7 {errorText}"
            : $"{_tableName} \u00b7 {_lastTotalRowCount} {rowsText}";
        PageSummary = _localizationService.FormatLocalizedString("VmPageSummary", CurrentPage, TotalPages);
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

    private void ApplyPage(int requestedPage)
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

        PageSummary = _localizationService.FormatLocalizedString("VmPageSummary", currentPage, totalPages);
        CanGoPreviousPage = currentPage > 1;
        CanGoNextPage = currentPage < totalPages;
    }
}
