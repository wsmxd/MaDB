using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Desktop.Services;

namespace MaDB.Desktop.ViewModels;

public partial class ConnectionManagerViewModel : ViewModelBase
{
    private DatabaseWorkspaceService _workspaceService;
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

    public void UpdateWorkspace(DatabaseWorkspaceService workspaceService, string connectionName = "Local SQLite")
    {
        _workspaceService = workspaceService;
        
        // Update the first connection card with new workspace info
        if (Connections.Count > 0)
        {
            Connections[0] = new ConnectionCardViewModel(
                connectionName,
                _workspaceService.DatabasePath,
                "SQLite",
                _workspaceService.AccessMode.ToString().ToLowerInvariant(),
                "Live database workspace",
                true);
        }
        
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        ConnectionSummary = _workspaceService.ConnectionSummary;
        QueryModeSummary = _workspaceService.AccessMode.ToString().ToLowerInvariant();
    }
}