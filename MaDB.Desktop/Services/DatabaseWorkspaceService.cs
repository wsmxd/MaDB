using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MaDB.Core;
using MaDB.Core.Schema;
using MaDB.Core.Sqlite;
using MaDB.Desktop.Models;

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
        var schema = await ReadSchemaAsync(cancellationToken);
        await UpdateTableRowAsync(tableName, values, schema, cancellationToken);
    }

    public async Task UpdateTableRowAsync(
        string tableName,
        IReadOnlyDictionary<string, string> values,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

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

    public async Task InsertTableRowAsync(
        string tableName,
        IReadOnlyDictionary<string, string> values,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var table = schema.Tables.FirstOrDefault(t => t.Name == tableName);

        if (table is null)
        {
            throw new InvalidOperationException($"Table '{tableName}' not found.");
        }

        var insertColumns = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var parameterNames = new List<string>();
        var paramIndex = 0;

        foreach (var column in table.Columns.OrderBy(column => column.OrdinalPosition))
        {
            values.TryGetValue(column.Name, out var value);

            if (column.IsAutoIncrement && string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var paramName = $"@p{paramIndex++}";
            insertColumns.Add(QuoteIdentifier(column.Name));
            parameterNames.Add(paramName);
            parameters[paramName] = value ?? string.Empty;
        }

        var sql = insertColumns.Count == 0
            ? $"INSERT INTO {QuoteIdentifier(tableName)} DEFAULT VALUES;"
            : $"INSERT INTO {QuoteIdentifier(tableName)} ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", parameterNames)});";

        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
    }

    public async Task DeleteTableRowAsync(
        string tableName,
        IReadOnlyDictionary<string, string> values,
        DatabaseSchema schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var table = schema.Tables.FirstOrDefault(t => t.Name == tableName);

        if (table is null)
        {
            throw new InvalidOperationException($"Table '{tableName}' not found.");
        }

        var keyColumns = table.Columns
            .Where(column => column.IsPrimaryKey)
            .OrderBy(column => column.OrdinalPosition)
            .ToList();

        if (keyColumns.Count == 0)
        {
            keyColumns = table.Columns
                .OrderBy(column => column.OrdinalPosition)
                .ToList();
        }

        if (keyColumns.Count == 0)
        {
            throw new InvalidOperationException($"Table '{tableName}' has no columns to build a delete predicate.");
        }

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var paramIndex = 0;

        foreach (var column in keyColumns)
        {
            values.TryGetValue(column.Name, out var value);
            var paramName = $"@p{paramIndex++}";
            whereClauses.Add($"{QuoteIdentifier(column.Name)} = {paramName}");
            parameters[paramName] = value ?? string.Empty;
        }

        var sql = $"DELETE FROM {QuoteIdentifier(tableName)} WHERE {string.Join(" AND ", whereClauses)};";
        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
    }

    public async Task CreateTableAsync(
        TableDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        var sql = BuildCreateTableSql(definition);
        await ExecuteNonQueryAsync(sql, cancellationToken: cancellationToken);
    }

    public async Task DeleteTableAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        var sql = $"DROP TABLE {QuoteIdentifier(tableName)};";
        await ExecuteNonQueryAsync(sql, cancellationToken: cancellationToken);
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

    private static string BuildCreateTableSql(TableDefinition definition)
    {
        var tableName = definition.TableName?.Trim();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(definition));
        }

        if (definition.Columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(definition));
        }

        var columnDefinitions = new List<string>();
        var primaryKeyColumns = definition.Columns.Where(column => column.IsPrimaryKey).ToList();

        foreach (var column in definition.Columns)
        {
            var columnName = column.Name?.Trim();
            var dataType = column.DataType?.Trim();

            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Column name is required.", nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(dataType))
            {
                throw new ArgumentException($"Column '{columnName}' must have a data type.", nameof(definition));
            }

            var columnParts = new List<string>
            {
                QuoteIdentifier(columnName),
                dataType
            };

            if (column.IsAutoIncrement)
            {
                if (!column.IsPrimaryKey)
                {
                    throw new ArgumentException($"Auto increment column '{columnName}' must be a primary key.", nameof(definition));
                }

                if (!string.Equals(dataType, "INTEGER", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Auto increment column '{columnName}' must use INTEGER data type.", nameof(definition));
                }

                columnParts.Add("PRIMARY KEY AUTOINCREMENT");
            }
            else
            {
                if (!column.IsNullable)
                {
                    columnParts.Add("NOT NULL");
                }

                if (primaryKeyColumns.Count == 1 && column.IsPrimaryKey)
                {
                    columnParts.Add("PRIMARY KEY");
                }

                if (!string.IsNullOrWhiteSpace(column.DefaultValue))
                {
                    columnParts.Add($"DEFAULT {column.DefaultValue.Trim()}");
                }
            }

            columnDefinitions.Add(string.Join(" ", columnParts));
        }

        if (primaryKeyColumns.Count > 1)
        {
            var primaryKeyList = string.Join(", ", primaryKeyColumns.Select(column => QuoteIdentifier(column.Name.Trim())));
            columnDefinitions.Add($"PRIMARY KEY ({primaryKeyList})");
        }

        return $"CREATE TABLE {QuoteIdentifier(tableName)} (\n  {string.Join(",\n  ", columnDefinitions)}\n);";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"") }\"";
    }
}