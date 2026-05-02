using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Core.Sqlite;

namespace MaDB.Desktop.Services;

public sealed class DatabaseWorkspaceService
{
    private readonly DatabaseProviderRegistry _providerRegistry;
    private readonly DatabaseDialect _dialect;
    private readonly string _databasePath;
    private readonly DatabaseAccessMode _accessMode;

    public DatabaseWorkspaceService(string databasePath, DatabaseAccessMode accessMode = DatabaseAccessMode.ReadWrite)
    {
        _databasePath = databasePath;
        _accessMode = accessMode;
        _dialect = DatabaseDialect.Sqlite;
        _providerRegistry = new DatabaseProviderRegistry([new SqliteDatabaseProvider()]);
    }

    public string DatabasePath => _databasePath;

    public string DatabaseFileName => Path.GetFileName(_databasePath);

    public DatabaseDialect Dialect => _dialect;

    public DatabaseAccessMode AccessMode => _accessMode;

    public string ConnectionSummary => $"{DatabaseFileName} · SQLite";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath) ?? AppContext.BaseDirectory);

        if (!File.Exists(_databasePath))
        {
            await CreateEmptyDatabaseAsync(cancellationToken);
        }
    }

    public async Task CreateEmptyDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var executor = CreateExecutor();
        await executor.ExecuteQueryAsync("SELECT 1;", cancellationToken: cancellationToken);
    }

    public async Task<DatabaseSchema> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        var schemaReader = CreateSchemaReader();
        return await schemaReader.ReadSchemaAsync(cancellationToken);
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var executor = CreateExecutor();
        return await executor.ExecuteQueryAsync(sql, parameters, cancellationToken);
    }

    public async Task<QueryResult> ReadTableRowsAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var sql = $"SELECT * FROM {QuoteIdentifier(tableName)};";
        return await ExecuteQueryAsync(sql, cancellationToken: cancellationToken);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var executor = CreateExecutor();
        return await executor.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
    }

    public async Task UpdateTableRowAsync(
        string tableName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var schema = await ReadSchemaAsync(cancellationToken);
        var table = schema.Tables.FirstOrDefault(t => t.Name == tableName);
        
        if (table is null)
        {
            throw new InvalidOperationException($"Table '{tableName}' not found.");
        }

        var primaryKeyColumns = table.Columns
            .Where(c => c.IsPrimaryKey)
            .Select(c => c.Name)
            .ToList();

        if (primaryKeyColumns.Count == 0)
        {
            throw new InvalidOperationException($"Table '{tableName}' has no primary key. Cannot update rows.");
        }

        var setClauses = new List<string>();
        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var paramIndex = 0;

        foreach (var kvp in values)
        {
            var paramName = $"@p{paramIndex++}";
            
            if (primaryKeyColumns.Contains(kvp.Key))
            {
                whereClauses.Add($"{QuoteIdentifier(kvp.Key)} = {paramName}");
                parameters[paramName] = kvp.Value;
            }
            else
            {
                setClauses.Add($"{QuoteIdentifier(kvp.Key)} = {paramName}");
                parameters[paramName] = kvp.Value;
            }
        }

        if (setClauses.Count == 0)
        {
            return;
        }

        var sql = $"UPDATE {QuoteIdentifier(tableName)} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
    }

    private IQueryExecutor CreateExecutor()
    {
        return _providerRegistry.CreateExecutor(CreateConnectionOptions());
    }

    private ISchemaReader CreateSchemaReader()
    {
        return _providerRegistry.CreateSchemaReader(CreateConnectionOptions());
    }

    private DatabaseConnectionOptions CreateConnectionOptions()
    {
        return _providerRegistry.CreateConnectionOptions(_dialect, _databasePath, _accessMode);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";    }
}