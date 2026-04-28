using Microsoft.Data.Sqlite;

namespace MaDB.Core.Sqlite;

public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    public DatabaseDialect Dialect => DatabaseDialect.Sqlite;

    public DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("SQLite target file is required.", nameof(target));
        }

        return new DatabaseConnectionOptions(
            DatabaseDialect.Sqlite,
            $"Data Source={target}",
            accessMode);
    }

    public IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.Sqlite)
        {
            throw new ArgumentException("Sqlite provider can only handle sqlite options.", nameof(options));
        }

        return new SqliteQueryExecutor(options.ConnectionString, options.AccessMode);
    }
}

public sealed class SqliteQueryExecutor(string connectionString, DatabaseAccessMode accessMode) : IQueryExecutor
{
    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureWritable();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                var value = reader[column];
                row[column] = value is DBNull ? null : value;
            }
            rows.Add(row);
        }

        return new QueryResult(columns, rows);
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = accessMode == DatabaseAccessMode.ReadOnly
                ? SqliteOpenMode.ReadOnly
                : SqliteOpenMode.ReadWriteCreate
        };

        return new SqliteConnection(builder.ConnectionString);
    }

    private void EnsureWritable()
    {
        if (accessMode == DatabaseAccessMode.ReadOnly)
        {
            throw new InvalidOperationException("Current connection is read-only.");
        }
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters is null)
        {
            return command;
        }

        foreach (var (name, value) in parameters)
        {
            var parameterName = name.StartsWith('@') ? name : $"@{name}";
            command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
        }

        return command;
    }
}
