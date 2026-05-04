using System;
using System.IO;
using MaDB.Core;

namespace MaDB.Desktop.Models;

public class DatabaseConnectionInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DatabaseDialect Dialect { get; set; } = DatabaseDialect.Sqlite;
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public DatabaseAccessMode AccessMode { get; set; } = DatabaseAccessMode.ReadWrite;
    public bool IsDefault { get; set; }
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    public string DisplaySummary => Dialect switch
    {
        DatabaseDialect.Sqlite => DatabasePath,
        _ => ConnectionString
    };

    public string DisplayName => Dialect switch
    {
        DatabaseDialect.Sqlite => Path.GetFileName(DatabasePath),
        _ => Name
    };

    public string DialectDisplay => Dialect switch
    {
        DatabaseDialect.Sqlite => "SQLite",
        DatabaseDialect.MySql => "MySQL",
        DatabaseDialect.PostgreSql => "PostgreSQL",
        _ => Dialect.ToString()
    };
}
