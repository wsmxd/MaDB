using Microsoft.Data.Sqlite;
using MaDB.Core.Schema;
using MaDB.Core.Transfer;

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

    public ISchemaReader CreateSchemaReader(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.Sqlite)
        {
            throw new ArgumentException("Sqlite provider can only handle sqlite options.", nameof(options));
        }

        return new SqliteSchemaReader(options.ConnectionString, options.AccessMode);
    }

    public IDatabaseImportExportService CreateImportExportService(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.Sqlite)
        {
            throw new ArgumentException("Sqlite provider can only handle sqlite options.", nameof(options));
        }

        return new SqliteImportExportService(options.ConnectionString, options.AccessMode);
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

public sealed class SqliteSchemaReader(string connectionString, DatabaseAccessMode accessMode) : ISchemaReader
{
    public async Task<DatabaseSchema> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var tables = new List<TableSchema>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT name, type, sql
            FROM sqlite_master
            WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var type = reader.GetString(1);
            var definitionSql = reader.IsDBNull(2) ? null : reader.GetString(2);
            var columns = type == "table"
                ? await ReadColumnsAsync(connection, name, cancellationToken)
                : [];
            tables.Add(new TableSchema(name, type, columns, definitionSql));
        }

        return new DatabaseSchema(tables);
    }

    private static async Task<IReadOnlyList<ColumnSchema>> ReadColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\");";
        await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnSchema(
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(3) == 0,
                reader.GetInt32(5) > 0,
                reader.IsDBNull(4) ? null : reader.GetValue(4)?.ToString()));
        }

        return columns;
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
}

public sealed class SqliteImportExportService(string connectionString, DatabaseAccessMode accessMode) : IDatabaseImportExportService
{
    public async Task<DatabaseTransferResult> ExportAsync(
        DatabaseExportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Format != DatabaseTransferFormat.Sql)
        {
            throw new NotSupportedException($"Unsupported export format: {options.Format}");
        }

        await using var connection = CreateConnection(readOnlyPreferred: true);
        await connection.OpenAsync(cancellationToken);

        var lines = new List<string>();
        var tables = new List<string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT name, sql
                FROM sqlite_master
                WHERE type='table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
                if (!reader.IsDBNull(1))
                {
                    lines.Add($"{reader.GetString(1)};");
                    lines.Add(string.Empty);
                }
            }
        }

        foreach (var table in tables)
        {
            await using var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\";";
            await using var dataReader = await dataCmd.ExecuteReaderAsync(cancellationToken);

            while (await dataReader.ReadAsync(cancellationToken))
            {
                var values = new string[dataReader.FieldCount];
                for (var i = 0; i < dataReader.FieldCount; i++)
                {
                    values[i] = ToSqlLiteral(dataReader.GetValue(i));
                }

                lines.Add($"INSERT INTO \"{table.Replace("\"", "\"\"")}\" VALUES ({string.Join(", ", values)});");
            }

            lines.Add(string.Empty);
        }

        await File.WriteAllLinesAsync(options.OutputPath, lines, cancellationToken);
        return new DatabaseTransferResult(true, options.OutputPath, lines.Count(l => l.EndsWith(';')));
    }

    public async Task<DatabaseTransferResult> ImportAsync(
        DatabaseImportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (accessMode == DatabaseAccessMode.ReadOnly)
        {
            throw new InvalidOperationException("Current connection is read-only.");
        }

        if (options.Format != DatabaseTransferFormat.Sql)
        {
            throw new NotSupportedException($"Unsupported import format: {options.Format}");
        }

        if (!File.Exists(options.InputPath))
        {
            throw new FileNotFoundException("Import file not found.", options.InputPath);
        }

        var script = await File.ReadAllTextAsync(options.InputPath, cancellationToken);
        var statements = SplitSqlStatements(script);

        await using var connection = CreateConnection(readOnlyPreferred: false);
        await connection.OpenAsync(cancellationToken);

        var processed = 0;
        foreach (var statement in statements)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            processed++;
        }

        return new DatabaseTransferResult(true, options.InputPath, processed);
    }

    private SqliteConnection CreateConnection(bool readOnlyPreferred)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = readOnlyPreferred || accessMode == DatabaseAccessMode.ReadOnly
                ? SqliteOpenMode.ReadOnly
                : SqliteOpenMode.ReadWriteCreate
        };

        return new SqliteConnection(builder.ConnectionString);
    }

    private static List<string> SplitSqlStatements(string script)
    {
        var statements = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inSingle = false;
        var inDouble = false;

        for (var i = 0; i < script.Length; i++)
        {
            var ch = script[i];
            var next = i + 1 < script.Length ? script[i + 1] : '\0';

            if (!inSingle && !inDouble && ch == '-' && next == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (!inSingle && !inDouble && ch == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }
                i++;
                continue;
            }

            if (ch == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (ch == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }

            if (ch == ';' && !inSingle && !inDouble)
            {
                var statement = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    statements.Add(statement);
                }
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        var tail = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
        {
            statements.Add(tail);
        }

        return statements;
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value is null or DBNull)
        {
            return "NULL";
        }

        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            byte[] bytes => $"X'{Convert.ToHexString(bytes)}'",
            DateTime dt => $"'{dt:O}'",
            DateTimeOffset dto => $"'{dto:O}'",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
    }
}
