using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Core;

namespace MaDB.Desktop.ViewModels;

public sealed partial class QueryResultGridRow : ObservableObject
{
    private readonly Dictionary<string, string> _values;

    public QueryResultGridRow(IReadOnlyDictionary<string, string> values)
    {
        _values = new Dictionary<string, string>(values);
    }

    public string GetValue(string columnName)
    {
        return _values.TryGetValue(columnName, out var value) ? value : string.Empty;
    }

    public void SetValue(string columnName, string value)
    {
        if (_values.TryGetValue(columnName, out var existing) && existing == value)
        {
            return;
        }
        _values[columnName] = value;
        OnPropertyChanged("Item[]");
    }

    public IReadOnlyDictionary<string, string> GetAllValues()
    {
        return _values;
    }

    public string this[string columnName]
    {
        get => GetValue(columnName);
        set => SetValue(columnName, value);
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
