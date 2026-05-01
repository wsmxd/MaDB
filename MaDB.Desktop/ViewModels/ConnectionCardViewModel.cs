namespace MaDB.Desktop.ViewModels;

public sealed record ConnectionCardViewModel(
    string Name,
    string Target,
    string Dialect,
    string Mode,
    string Details,
    bool IsActive);