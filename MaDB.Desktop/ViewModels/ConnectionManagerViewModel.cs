using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class ConnectionManagerViewModel : ViewModelBase
{
    private readonly DatabaseWorkspaceService _workspaceService;
    private readonly ILocalizationService _localizationService;

    public ConnectionManagerViewModel(
        DatabaseWorkspaceService workspaceService,
        ILocalizationService localizationService)
    {
        _workspaceService = workspaceService;
        _localizationService = localizationService;

        ConnectionSummary = _workspaceService.ConnectionSummary;
        QueryModeSummary = _workspaceService.AccessMode.ToString().ToLowerInvariant();

        Connections =
        [
            new ConnectionCardViewModel(
                "Local SQLite",
                _workspaceService.DatabasePath,
                "SQLite",
                QueryModeSummary,
                "Live database workspace",
                true),
            new ConnectionCardViewModel(
                "MySQL (future)",
                "not connected",
                "MySql",
                "readwrite",
                "Reserved for provider expansion.",
                false),
            new ConnectionCardViewModel(
                "PostgreSQL (future)",
                "not connected",
                "PostgreSql",
                "readwrite",
                "Reserved for provider expansion.",
                false)
        ];
    }

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private string _queryModeSummary = string.Empty;

    public ObservableCollection<ConnectionCardViewModel> Connections { get; }

    public void RefreshLocalizedText()
    {
        ConnectionSummary = _workspaceService.ConnectionSummary;
        QueryModeSummary = _workspaceService.AccessMode.ToString().ToLowerInvariant();
    }
}