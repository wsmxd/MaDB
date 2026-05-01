namespace MaDB.Desktop.ViewModels;

public sealed record DatabaseTableViewModel(
    string Name,
    string Type,
    int ColumnCount,
    string? DefinitionSql)
{
    public string ColumnSummary => ColumnCount == 1 ? "1 column" : $"{ColumnCount} columns";

    public string KindSummary => Type;
}
