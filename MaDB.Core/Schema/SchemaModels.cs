namespace MaDB.Core.Schema;

public enum TableType
{
    Table = 1,
    View = 2,
    MaterializedView = 3
}

public sealed record DatabaseSchema(IReadOnlyList<TableSchema> Tables);

public sealed record TableSchema(
    string Name,
    TableType Type,
    IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<IndexSchema> Indexes,
    IReadOnlyList<ForeignKeySchema> ForeignKeys,
    string? DefinitionSql);

public sealed record ColumnSchema(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? DefaultValue,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool IsAutoIncrement,
    int OrdinalPosition);

public sealed record IndexSchema(
    string Name,
    bool IsUnique,
    bool IsPrimary,
    IReadOnlyList<IndexColumnSchema> Columns);

public sealed record IndexColumnSchema(
    string ColumnName,
    int OrdinalPosition,
    bool IsDescending);

public sealed record ForeignKeySchema(
    string Name,
    string ReferencedTable,
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<string> ReferencedColumnNames,
    string? OnDeleteAction,
    string? OnUpdateAction);
