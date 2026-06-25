using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MaDB.Core;
using MaDB.Desktop.Models;
using Microsoft.Data.Sqlite;

namespace MaDB.Desktop.Services;

public class ConnectionManagerService
{
    private readonly string _configPath;
    private List<DatabaseConnectionInfo> _connections = [];
    private DatabaseConnectionInfo? _activeConnection;

    public ConnectionManagerService()
    {
        var configDir = GetAppDataDir("Desktop");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "connections.json");
    }

    public IReadOnlyList<DatabaseConnectionInfo> Connections => _connections;
    public DatabaseConnectionInfo? ActiveConnection => _activeConnection;

    public event EventHandler? ConnectionsChanged;
    public event EventHandler<DatabaseConnectionInfo>? ActiveConnectionChanged;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _connections = JsonSerializer.Deserialize<List<DatabaseConnectionInfo>>(json) ?? [];
            }
        }
        catch
        {
            _connections = [];
        }

        if (_connections.Count == 0)
        {
            var defaultConnection = CreateDefaultConnection();
            _connections.Add(defaultConnection);
            await SaveAsync();
        }

        _activeConnection = _connections.FirstOrDefault(c => c.IsDefault) ?? _connections.FirstOrDefault();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_connections, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch
        {
        }
    }

    public async Task<DatabaseConnectionInfo> AddConnectionAsync(DatabaseConnectionInfo connection)
    {
        _connections.Add(connection);
        
        if (_connections.Count == 1)
        {
            connection.IsDefault = true;
            _activeConnection = connection;
        }

        await SaveAsync();
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        return connection;
    }

    public async Task UpdateConnectionAsync(DatabaseConnectionInfo connection)
    {
        var existing = _connections.FirstOrDefault(c => c.Id == connection.Id);
        if (existing is not null)
        {
            var index = _connections.IndexOf(existing);
            _connections[index] = connection;
            
            if (_activeConnection?.Id == connection.Id)
            {
                _activeConnection = connection;
            }

            await SaveAsync();
            ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        _connections.RemoveAll(c => c.Id == connectionId);
        
        if (_activeConnection?.Id == connectionId)
        {
            _activeConnection = _connections.FirstOrDefault();
            if (_activeConnection is not null)
            {
                _activeConnection.IsDefault = true;
            }
        }

        await SaveAsync();
        ConnectionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<DatabaseWorkspaceService> ConnectAsync(DatabaseConnectionInfo connection)
    {
        var target = connection.Dialect switch
        {
            DatabaseDialect.Sqlite => connection.DatabasePath,
            _ => connection.ConnectionString
        };

        var workspace = new DatabaseWorkspaceService(target, connection.Dialect, connection.AccessMode);

        await workspace.InitializeAsync();
        
        connection.LastUsedAt = DateTime.UtcNow;
        _activeConnection = connection;
        
        foreach (var c in _connections)
        {
            c.IsDefault = c.Id == connection.Id;
        }
        
        await SaveAsync();
        ActiveConnectionChanged?.Invoke(this, connection);
        
        return workspace;
    }

    public async Task<DatabaseWorkspaceService> ConnectToDefaultAsync()
    {
        var connection = _activeConnection ?? _connections.FirstOrDefault() ?? CreateDefaultConnection();
        return await ConnectAsync(connection);
    }

    public async Task<string> BrowseForSqliteFileAsync()
    {
        // This will be called from the view layer
        return string.Empty;
    }

    private static DatabaseConnectionInfo CreateDefaultConnection()
    {
        var dbDir = GetLocalDataDir("Databases");
        Directory.CreateDirectory(dbDir);
        
        var dbPath = Path.Combine(dbDir, "default.sqlite");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        
        return new DatabaseConnectionInfo
        {
            Name = "Default SQLite",
            Dialect = DatabaseDialect.Sqlite,
            DatabasePath = dbPath,
            ConnectionString = connectionString,
            AccessMode = DatabaseAccessMode.ReadWrite,
            IsDefault = true
        };
    }

    private static string GetAppDataDir(string subPath)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.GetTempPath();
        }

        return Path.Combine(baseDir, "MaDB", subPath);
    }

    private static string GetLocalDataDir(string subPath)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.GetTempPath();
        }

        return Path.Combine(baseDir, "MaDB", "Desktop", subPath);
    }
}
