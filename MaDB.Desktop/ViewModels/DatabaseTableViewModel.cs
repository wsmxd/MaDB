using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public sealed class DatabaseTableViewModel
{
    private readonly ILocalizationService _localizationService;

    public DatabaseTableViewModel(
        string name,
        string type,
        int columnCount,
        string? definitionSql,
        ILocalizationService localizationService)
    {
        Name = name;
        Type = type;
        ColumnCount = columnCount;
        DefinitionSql = definitionSql;
        _localizationService = localizationService;
    }

    public string Name { get; }
    public string Type { get; }
    public int ColumnCount { get; }
    public string? DefinitionSql { get; }

    public string ColumnSummary
    {
        get
        {
            var columnText = ColumnCount == 1
                ? _localizationService.GetLocalizedString("VmColumn") ?? "column"
                : _localizationService.GetLocalizedString("VmColumns") ?? "columns";
            return $"{ColumnCount} {columnText}";
        }
    }

    public string KindSummary => Type;
}
