using System.Collections.Generic;

namespace MaDB.Desktop.Models;

public sealed record TableDefinition(
    string TableName,
    IReadOnlyList<TableColumnDefinition> Columns);

public sealed record TableColumnDefinition(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsAutoIncrement,
    string? DefaultValue);