using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaDB.Core;
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

        var dialectLabel = _workspaceService.Dialect switch
        {
            DatabaseDialect.Sqlite => "SQLite",
            DatabaseDialect.MySql => "MySQL",
            DatabaseDialect.PostgreSql => "PostgreSQL",
            _ => _workspaceService.Dialect.ToString()
        };

        Connections =
        [
            CreateActiveCard("Active Connection", _workspaceService.DatabasePath, dialectLabel)
        ];
    }

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private string _queryModeSummary = string.Empty;

    public ObservableCollection<ConnectionCardViewModel> Connections { get; }

    public void UpdateWorkspace(DatabaseWorkspaceService workspaceService, string connectionName = "Active Connection")
    {
        _workspaceService = workspaceService;

        var dialectLabel = workspaceService.Dialect switch
        {
            DatabaseDialect.Sqlite => "SQLite",
            DatabaseDialect.MySql => "MySQL",
            DatabaseDialect.PostgreSql => "PostgreSQL",
            _ => workspaceService.Dialect.ToString()
        };

        var target = workspaceService.Dialect == DatabaseDialect.Sqlite
            ? workspaceService.DatabasePath
            : workspaceService.ConnectionString;

        Connections[0] = CreateActiveCard(connectionName, target, dialectLabel);

        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        ConnectionSummary = _workspaceService.ConnectionSummary;
        QueryModeSummary = _workspaceService.AccessMode.ToString().ToLowerInvariant();
    }

    private ConnectionCardViewModel CreateActiveCard(string name, string target, string dialect)
    {
        return new ConnectionCardViewModel(
            name,
            string.IsNullOrEmpty(target) ? "not connected" : target,
            dialect,
            _workspaceService.AccessMode.ToString().ToLowerInvariant(),
            "Active database connection",
            true);
    }
}