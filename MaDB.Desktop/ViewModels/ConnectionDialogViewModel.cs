using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaDB.Core;
using MaDB.Desktop.Models;
using MaDB.Desktop.Services;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace MaDB.Desktop.ViewModels;

public partial class ConnectionDialogViewModel : ViewModelBase
{
    private readonly ConnectionManagerService _connectionManager;
    private readonly ILocalizationService _localizationService;
    private DatabaseConnectionInfo? _editingConnection;

    public ConnectionDialogViewModel(
        ConnectionManagerService connectionManager,
        ILocalizationService localizationService)
    {
        _connectionManager = connectionManager;
        _localizationService = localizationService;
        
        AvailableDialects = [DatabaseDialect.Sqlite, DatabaseDialect.MySql, DatabaseDialect.PostgreSql];
        SelectedDialect = DatabaseDialect.Sqlite;
        
        LoadConnections();
    }

    public ObservableCollection<DatabaseConnectionInfo> SavedConnections { get; } = [];
    public DatabaseDialect[] AvailableDialects { get; }

    [ObservableProperty]
    private DatabaseDialect _selectedDialect;

    [ObservableProperty]
    private string _connectionName = string.Empty;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _port = string.Empty;

    [ObservableProperty]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _showSqliteOptions = true;

    [ObservableProperty]
    private bool _showServerOptions;

    [ObservableProperty]
    private DatabaseConnectionInfo? _selectedSavedConnection;

    partial void OnSelectedDialectChanged(DatabaseDialect value)
    {
        ShowSqliteOptions = value == DatabaseDialect.Sqlite;
        ShowServerOptions = value != DatabaseDialect.Sqlite;

        if (value != DatabaseDialect.Sqlite && string.IsNullOrEmpty(Port))
        {
            Port = value == DatabaseDialect.MySql ? "3306" : "5432";
        }
    }

    private void LoadConnections()
    {
        SavedConnections.Clear();
        foreach (var conn in _connectionManager.Connections)
        {
            SavedConnections.Add(conn);
        }
    }

    [RelayCommand]
    private void NewConnection()
    {
        _editingConnection = null;
        IsEditing = false;
        ConnectionName = string.Empty;
        DatabasePath = string.Empty;
        ConnectionString = string.Empty;
        Host = string.Empty;
        Port = string.Empty;
        DatabaseName = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        SelectedDialect = DatabaseDialect.Sqlite;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void EditConnection(DatabaseConnectionInfo? connection)
    {
        if (connection is null) return;
        
        _editingConnection = connection;
        IsEditing = true;
        ConnectionName = connection.Name;
        SelectedDialect = connection.Dialect;
        DatabasePath = connection.DatabasePath;
        ConnectionString = connection.ConnectionString;

        if (connection.Dialect != DatabaseDialect.Sqlite)
        {
            try
            {
                if (connection.Dialect == DatabaseDialect.PostgreSql)
                {
                    var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
                    Host = builder.Host ?? string.Empty;
                    Port = builder.Port.ToString();
                    DatabaseName = builder.Database ?? string.Empty;
                    Username = builder.Username ?? string.Empty;
                    Password = builder.Password ?? string.Empty;
                }
                else
                {
                    var builder = new MySqlConnectionStringBuilder(connection.ConnectionString);
                    Host = builder.Server;
                    Port = builder.Port.ToString();
                    DatabaseName = builder.Database;
                    Username = builder.UserID;
                    Password = builder.Password;
                }
            }
            catch
            {
                Host = string.Empty;
                Port = connection.Dialect == DatabaseDialect.MySql ? "3306" : "5432";
                DatabaseName = string.Empty;
                Username = string.Empty;
                Password = string.Empty;
            }
        }

        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveConnection(DatabaseConnectionInfo? connection)
    {
        if (connection is null) return;
        
        await _connectionManager.RemoveConnectionAsync(connection.Id);
        LoadConnections();
        
        if (SavedConnections.Count == 0)
        {
            NewConnection();
        }
    }

    [RelayCommand]
    private async Task BrowseFile()
    {
        // File browser will be handled from the view
        await Task.CompletedTask;
    }

    public DatabaseConnectionInfo BuildConnectionInfo()
    {
        var info = _editingConnection ?? new DatabaseConnectionInfo();
        
        info.Name = string.IsNullOrWhiteSpace(ConnectionName) 
            ? $"{SelectedDialect} Database" 
            : ConnectionName.Trim();
        info.Dialect = SelectedDialect;
        info.AccessMode = DatabaseAccessMode.ReadWrite;
        
        if (SelectedDialect == DatabaseDialect.Sqlite)
        {
            info.DatabasePath = DatabasePath.Trim();
            info.ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath.Trim() }.ToString();
        }
        else if (SelectedDialect == DatabaseDialect.PostgreSql)
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = Host.Trim(),
                Database = DatabaseName.Trim(),
                Username = Username.Trim(),
                Password = Password
            };

            if (int.TryParse(Port, out var port) && port > 0)
            {
                builder.Port = port;
            }

            info.ConnectionString = builder.ConnectionString;
        }
        else
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = Host.Trim(),
                Database = DatabaseName.Trim(),
                UserID = Username.Trim(),
                Password = Password
            };

            if (int.TryParse(Port, out var port) && port > 0)
            {
                builder.Port = (uint)port;
            }

            info.ConnectionString = builder.ConnectionString;
        }
        
        return info;
    }

    public bool Validate()
    {
        if (SelectedDialect == DatabaseDialect.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(DatabasePath))
            {
                ErrorMessage = _localizationService.GetLocalizedString("VmDatabasePathRequired") ?? "Database file path is required.";
                return false;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                ErrorMessage = _localizationService.GetLocalizedString("VmHostRequired") ?? "Host is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(DatabaseName))
            {
                ErrorMessage = _localizationService.GetLocalizedString("VmDatabaseNameRequired") ?? "Database name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = _localizationService.GetLocalizedString("VmUsernameRequired") ?? "Username is required.";
                return false;
            }
        }
        
        ErrorMessage = string.Empty;
        return true;
    }

    public async Task SaveConnectionAsync()
    {
        var info = BuildConnectionInfo();
        
        if (_editingConnection is not null)
        {
            await _connectionManager.UpdateConnectionAsync(info);
        }
        else
        {
            await _connectionManager.AddConnectionAsync(info);
        }
        
        LoadConnections();
    }
}
