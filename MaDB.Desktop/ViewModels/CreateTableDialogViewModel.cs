using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Core;
using MaDB.Desktop.Models;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class CreateTableDialogViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;

    public CreateTableDialogViewModel(ILocalizationService localizationService, DatabaseDialect dialect = DatabaseDialect.Sqlite)
    {
        _localizationService = localizationService;
        var dataTypes = dialect switch
        {
            DatabaseDialect.MySql => TableColumnEditorViewModel.MySqlDataTypes,
            DatabaseDialect.PostgreSql => TableColumnEditorViewModel.PostgreSqlDataTypes,
            _ => TableColumnEditorViewModel.SQLiteDataTypes
        };
        AvailableDataTypes = dataTypes;
        Columns.Add(new TableColumnEditorViewModel(dataTypes));
    }

    public IReadOnlyList<string> AvailableDataTypes { get; }

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ObservableCollection<TableColumnEditorViewModel> Columns { get; } = [];

    public void AddColumn()
    {
        Columns.Add(new TableColumnEditorViewModel(AvailableDataTypes));
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

            var dt = autoIncrementColumn.DataType.ToUpperInvariant();
            if (dt != "INTEGER" && dt != "INT" && dt != "BIGINT")
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
    public TableColumnEditorViewModel()
    {
        _availableDataTypes = SQLiteDataTypes;
    }

    public TableColumnEditorViewModel(IReadOnlyList<string> availableDataTypes)
    {
        _availableDataTypes = availableDataTypes;
    }

    [ObservableProperty]
    private IReadOnlyList<string> _availableDataTypes;

    public static IReadOnlyList<string> SQLiteDataTypes { get; } =
    [
        "INTEGER",
        "TEXT",
        "REAL",
        "BLOB",
        "NUMERIC",
        "BOOLEAN",
        "DATE",
        "DATETIME",
        "TIMESTAMP",
        "VARCHAR",
        "CHAR",
        "DECIMAL",
        "FLOAT",
        "DOUBLE",
        "INT",
        "BIGINT",
        "SMALLINT",
        "TINYINT",
        "CLOB"
    ];

    public static IReadOnlyList<string> MySqlDataTypes { get; } =
    [
        "INT",
        "BIGINT",
        "SMALLINT",
        "TINYINT",
        "VARCHAR(255)",
        "CHAR",
        "TEXT",
        "MEDIUMTEXT",
        "LONGTEXT",
        "BLOB",
        "FLOAT",
        "DOUBLE",
        "DECIMAL",
        "BOOLEAN",
        "DATE",
        "DATETIME",
        "TIMESTAMP",
        "TIME",
        "YEAR",
        "ENUM",
        "JSON"
    ];

    public static IReadOnlyList<string> PostgreSqlDataTypes { get; } =
    [
        "INTEGER",
        "BIGINT",
        "SMALLINT",
        "SERIAL",
        "BIGSERIAL",
        "VARCHAR(255)",
        "CHAR",
        "TEXT",
        "BYTEA",
        "REAL",
        "DOUBLE PRECISION",
        "DECIMAL",
        "NUMERIC",
        "BOOLEAN",
        "DATE",
        "TIMESTAMP",
        "TIMESTAMPTZ",
        "TIME",
        "INTERVAL",
        "UUID",
        "JSON",
        "JSONB",
        "ARRAY",
        "XML"
    ];

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
