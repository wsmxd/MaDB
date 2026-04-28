namespace MaDB.Core.Schema;

public sealed record DatabaseSchema(IReadOnlyList<TableSchema> Tables);

public sealed record TableSchema(
    string Name,
    string Type,
    IReadOnlyList<ColumnSchema> Columns,
    string? DefinitionSql);

public sealed record ColumnSchema(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? DefaultValue);
