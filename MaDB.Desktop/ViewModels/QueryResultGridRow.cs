using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MaDB.Core;

namespace MaDB.Desktop.ViewModels;

public sealed class QueryResultGridRow(IReadOnlyDictionary<string, string> values)
{
    public string GetValue(string columnName)
    {
        return values.TryGetValue(columnName, out var value) ? value : string.Empty;
    }
}

public sealed record QueryResultGrid(
    IReadOnlyList<string> Columns,
    ObservableCollection<QueryResultGridRow> Rows)
{
    public static QueryResultGrid Empty { get; } = new([], []);

    public static QueryResultGrid From(QueryResult result)
    {
        var rows = result.Rows
            .Select(row => new QueryResultGridRow(
                result.Columns.ToDictionary(
                    columnName => columnName,
                    columnName =>
                    {
                        row.TryGetValue(columnName, out var value);
                        return value?.ToString() ?? string.Empty;
                    })))
            .ToList();

        return new QueryResultGrid(
            result.Columns,
            new ObservableCollection<QueryResultGridRow>(rows));
    }
}
