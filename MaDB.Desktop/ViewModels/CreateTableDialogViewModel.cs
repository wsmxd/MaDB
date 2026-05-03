using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Desktop.Models;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class CreateTableDialogViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;

    public CreateTableDialogViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        Columns.Add(new TableColumnEditorViewModel());
    }

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ObservableCollection<TableColumnEditorViewModel> Columns { get; } = [];

    public void AddColumn()
    {
        Columns.Add(new TableColumnEditorViewModel());
        ErrorMessage = string.Empty;
    }

    public void RemoveColumn(TableColumnEditorViewModel? column)
    {
        if (column is null)
        {
            return;
        }

        Columns.Remove(column);
        if (Columns.Count == 0)
        {
            Columns.Add(new TableColumnEditorViewModel());
        }

        ErrorMessage = string.Empty;
    }

    public bool TryBuildDefinition(out TableDefinition? definition, out string errorMessage)
    {
        var tableName = TableName.Trim();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            errorMessage = _localizationService.GetLocalizedString("VmTableNameRequired") ?? "Table name is required.";
            ErrorMessage = errorMessage;
            definition = null;
            return false;
        }

        var columns = new List<TableColumnDefinition>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in Columns)
        {
            var columnName = column.Name.Trim();
            var dataType = column.DataType.Trim();

            if (string.IsNullOrWhiteSpace(columnName))
            {
                errorMessage = _localizationService.GetLocalizedString("VmColumnNameRequired") ?? "Each column needs a name.";
                ErrorMessage = errorMessage;
                definition = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(dataType))
            {
                errorMessage = _localizationService.FormatLocalizedString("VmColumnTypeRequired", columnName);
                ErrorMessage = errorMessage;
                definition = null;
                return false;
            }

            if (!seenNames.Add(columnName))
            {
                errorMessage = _localizationService.FormatLocalizedString("VmColumnDuplicated", columnName);
                ErrorMessage = errorMessage;
                definition = null;
                return false;
            }

            columns.Add(new TableColumnDefinition(
                columnName,
                dataType,
                column.IsNullable,
                column.IsPrimaryKey,
                column.IsAutoIncrement,
                string.IsNullOrWhiteSpace(column.DefaultValue) ? null : column.DefaultValue.Trim()));
        }

        if (columns.Count == 0)
        {
            errorMessage = _localizationService.GetLocalizedString("VmAtLeastOneColumn") ?? "At least one column is required.";
            ErrorMessage = errorMessage;
            definition = null;
            return false;
        }

        var autoIncrementColumn = columns.FirstOrDefault(column => column.IsAutoIncrement);
        if (autoIncrementColumn is not null)
        {
            if (!autoIncrementColumn.IsPrimaryKey)
            {
                errorMessage = _localizationService.FormatLocalizedString("VmAutoIncrementMustBePrimaryKey", autoIncrementColumn.Name);
                ErrorMessage = errorMessage;
                definition = null;
                return false;
            }

            if (!string.Equals(autoIncrementColumn.DataType, "INTEGER", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = _localizationService.FormatLocalizedString("VmAutoIncrementMustBeInteger", autoIncrementColumn.Name);
                ErrorMessage = errorMessage;
                definition = null;
                return false;
            }
        }

        definition = new TableDefinition(tableName, columns);
        ErrorMessage = string.Empty;
        errorMessage = string.Empty;
        return true;
    }
}

public partial class TableColumnEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _dataType = "TEXT";

    [ObservableProperty]
    private bool _isNullable = true;

    [ObservableProperty]
    private bool _isPrimaryKey;

    [ObservableProperty]
    private bool _isAutoIncrement;

    [ObservableProperty]
    private string _defaultValue = string.Empty;
}
