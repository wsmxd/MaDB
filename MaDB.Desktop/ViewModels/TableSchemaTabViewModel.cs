using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class TableSchemaTabViewModel : TabViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly string _tableName;

    public TableSchemaTabViewModel(
        string tableName,
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _tableName = tableName;
        _workspaceService = workspaceService;
        Title = $"{tableName} - Schema";
        Icon = "\u2637";
    }

    [ObservableProperty]
    private IReadOnlyList<ColumnInfo> _columns = [];

    [ObservableProperty]
    private string _tableType = string.Empty;

    [ObservableProperty]
    private string _definitionSql = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [RelayCommand]
    public async Task LoadSchemaAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            
            var schema = await _workspaceService.ReadSchemaAsync();
            var table = schema.Tables.FirstOrDefault(t => t.Name == _tableName);
            
            if (table is null)
            {
                ErrorMessage = $"Table '{_tableName}' not found.";
                return;
            }

            TableType = table.Type.ToString();
            DefinitionSql = table.DefinitionSql ?? string.Empty;
            
            var columns = new List<ColumnInfo>();
            foreach (var col in table.Columns)
            {
                columns.Add(new ColumnInfo
                {
                    Name = col.Name,
                    DataType = col.DataType,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = col.IsPrimaryKey,
                    DefaultValue = col.DefaultValue ?? string.Empty,
                    OrdinalPosition = col.OrdinalPosition
                });
            }
            
            Columns = columns;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
    
    public string NullableText => IsNullable ? "YES" : "NO";
    public string PrimaryKeyText => IsPrimaryKey ? "PK" : "";
}
