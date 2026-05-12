using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using MySqlConnector;
using MaDB.Core.Schema;
using MaDB.Core.Transfer;

namespace MaDB.Core.MySql;

public sealed class MySqlDatabaseProvider : IDatabaseProvider
{
    public DatabaseDialect Dialect => DatabaseDialect.MySql;

    public DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("MySQL connection string or target is required.", nameof(target));
        }

        var connectionString = target.Contains('=')
            ? target
            : BuildConnectionString(target);

        return new DatabaseConnectionOptions(DatabaseDialect.MySql, connectionString, accessMode);
    }

    public IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.MySql)
        {
            throw new ArgumentException("MySQL provider can only handle MySQL options.", nameof(options));
        }

        return new MySqlQueryExecutor(options.ConnectionString, options.AccessMode);
    }

    public ISchemaReader CreateSchemaReader(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.MySql)
        {
            throw new ArgumentException("MySQL provider can only handle MySQL options.", nameof(options));
        }

        return new MySqlSchemaReader(options.ConnectionString, options.AccessMode);
    }

    public IDatabaseImportExportService CreateImportExportService(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.MySql)
        {
            throw new ArgumentException("MySQL provider can only handle MySQL options.", nameof(options));
        }

        return new MySqlImportExportService(options.ConnectionString, options.AccessMode);
    }

    private static string BuildConnectionString(string target)
    {
        var host = target;
        int? port = null;
        string? database = null;

        var slashIndex = target.IndexOf('/');
        if (slashIndex >= 0)
        {
            host = target[..slashIndex];
            var afterSlash = target[(slashIndex + 1)..];
            var colonIndex = afterSlash.IndexOf(':');
            if (colonIndex >= 0)
            {
                database = afterSlash[..colonIndex];
                if (int.TryParse(afterSlash[(colonIndex + 1)..], out var p))
                {
                    port = p;
                }
            }
            else
            {
                database = afterSlash;
            }
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Database = database,
            UserID = "root"
        };

        if (port.HasValue)
        {
            builder.Port = (uint)port.Value;
        }

        return builder.ConnectionString;
    }
}

public sealed class MySqlQueryExecutor(string connectionString, DatabaseAccessMode accessMode) : IQueryExecutor
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

    public async Task<IQueryTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        EnsureWritable();

        var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new MySqlQueryTransaction(connection, transaction);
    }

    public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteQueryStreamAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                var value = reader[column];
                row[column] = value is DBNull ? null : value;
            }
            yield return row;
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private MySqlConnection CreateConnection()
    {
        return new MySqlConnection(connectionString);
    }

    private void EnsureWritable()
    {
        if (accessMode == DatabaseAccessMode.ReadOnly)
        {
            throw new InvalidOperationException("Current connection is read-only.");
        }
    }

    private static MySqlCommand CreateCommand(
        MySqlConnection connection,
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

public sealed class MySqlQueryTransaction : IQueryTransaction
{
    private readonly MySqlConnection _connection;
    private readonly MySqlTransaction _transaction;
    private bool _committed;
    private bool _rollbacked;

    internal MySqlQueryTransaction(MySqlConnection connection, MySqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _rollbacked)
        {
            throw new InvalidOperationException("Transaction has already been completed.");
        }

        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_committed || _rollbacked)
        {
            throw new InvalidOperationException("Transaction has already been completed.");
        }

        await _transaction.RollbackAsync(cancellationToken);
        _rollbacked = true;
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _transaction;

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                var parameterName = name.StartsWith('@') ? name : $"@{name}";
                command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
            }
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _transaction;

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                var parameterName = name.StartsWith('@') ? name : $"@{name}";
                command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
            }
        }

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

    public async ValueTask DisposeAsync()
    {
        if (!_committed && !_rollbacked)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
            }
        }

        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed class MySqlSchemaReader(string connectionString, DatabaseAccessMode accessMode) : ISchemaReader
{
    public async Task<DatabaseSchema> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var tableEntries = new List<(string Name, TableType Type, string? Comment)>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT TABLE_NAME, TABLE_TYPE, TABLE_COMMENT
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                ORDER BY TABLE_TYPE, TABLE_NAME;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var tableType = reader.GetString(1);
                var comment = reader.IsDBNull(2) ? null : reader.GetString(2);
                var type = tableType == "VIEW" ? TableType.View : TableType.Table;
                tableEntries.Add((name, type, comment));
            }
        }

        var tables = new List<TableSchema>();
        foreach (var entry in tableEntries)
        {
            var columns = await ReadColumnsAsync(connection, entry.Name, cancellationToken);
            var indexes = await ReadIndexesAsync(connection, entry.Name, cancellationToken);
            var foreignKeys = await ReadForeignKeysAsync(connection, entry.Name, cancellationToken);
            tables.Add(new TableSchema(entry.Name, entry.Type, columns, indexes, foreignKeys, entry.Comment));
        }

        return new DatabaseSchema(tables);
    }

    private static async Task<IReadOnlyList<ColumnSchema>> ReadColumnsAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                COLUMN_DEFAULT,
                ORDINAL_POSITION,
                CHARACTER_MAXIMUM_LENGTH,
                NUMERIC_PRECISION,
                NUMERIC_SCALE,
                EXTRA,
                COLUMN_KEY
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2) == "YES";
            var defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3);
            var ordinal = reader.GetInt32(4) - 1;
            var maxLength = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
            var precision = reader.IsDBNull(6) ? (int?)null : (int)reader.GetInt64(6);
            var scale = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
            var extra = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            var columnKey = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);

            var isPrimaryKey = columnKey == "PRI";
            var isAutoIncrement = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);

            columns.Add(new ColumnSchema(
                name,
                dataType,
                isNullable,
                isPrimaryKey,
                defaultValue,
                maxLength,
                precision,
                scale,
                isAutoIncrement,
                ordinal));
        }

        return columns;
    }

    private static async Task<IReadOnlyList<IndexSchema>> ReadIndexesAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var indexMap = new Dictionary<string, (bool IsUnique, bool IsPrimary, List<IndexColumnSchema> Columns)>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME, INDEX_TYPE
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table
            ORDER BY INDEX_NAME, SEQ_IN_INDEX;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var indexName = reader.GetString(0);
            var nonUnique = reader.GetInt32(1) == 1;
            var seqInIndex = reader.GetInt32(2);
            var columnName = reader.GetString(3);

            if (!indexMap.TryGetValue(indexName, out var entry))
            {
                var isPrimary = indexName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase);
                entry = (!nonUnique, isPrimary, []);
                indexMap[indexName] = entry;
            }

            entry.Columns.Add(new IndexColumnSchema(columnName, seqInIndex - 1, false));
        }

        return indexMap.Select(kv => new IndexSchema(
            kv.Key,
            kv.Value.IsUnique,
            kv.Value.IsPrimary,
            kv.Value.Columns)).ToList();
    }

    private static async Task<IReadOnlyList<ForeignKeySchema>> ReadForeignKeysAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var fkMap = new Dictionary<string, ForeignKeyEntry>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                k.CONSTRAINT_NAME,
                k.COLUMN_NAME,
                k.REFERENCED_TABLE_NAME,
                k.REFERENCED_COLUMN_NAME,
                r.UPDATE_RULE,
                r.DELETE_RULE
            FROM information_schema.KEY_COLUMN_USAGE k
            JOIN information_schema.REFERENTIAL_CONSTRAINTS r
                ON k.CONSTRAINT_NAME = r.CONSTRAINT_NAME
                AND k.CONSTRAINT_SCHEMA = r.CONSTRAINT_SCHEMA
            WHERE k.TABLE_SCHEMA = DATABASE()
                AND k.TABLE_NAME = @table
                AND k.REFERENCED_TABLE_NAME IS NOT NULL
            ORDER BY k.CONSTRAINT_NAME, k.ORDINAL_POSITION;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var constraintName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var refTable = reader.GetString(2);
            var refColumn = reader.GetString(3);
            var updateRule = reader.IsDBNull(4) ? null : reader.GetString(4);
            var deleteRule = reader.IsDBNull(5) ? null : reader.GetString(5);

            if (!fkMap.TryGetValue(constraintName, out var entry))
            {
                entry = new ForeignKeyEntry(refTable, updateRule, deleteRule);
                fkMap[constraintName] = entry;
            }

            entry.ColumnNames.Add(columnName);
            entry.ReferencedColumnNames.Add(refColumn);
        }

        return fkMap.Select(kv => new ForeignKeySchema(
            kv.Key,
            kv.Value.ReferencedTable,
            kv.Value.ColumnNames,
            kv.Value.ReferencedColumnNames,
            kv.Value.OnDeleteAction,
            kv.Value.OnUpdateAction)).ToList();
    }

    private MySqlConnection CreateConnection()
    {
        _ = accessMode;
        return new MySqlConnection(connectionString);
    }

    private sealed class ForeignKeyEntry(
        string referencedTable,
        string? onUpdateAction,
        string? onDeleteAction)
    {
        public string ReferencedTable { get; } = referencedTable;
        public string? OnUpdateAction { get; } = onUpdateAction;
        public string? OnDeleteAction { get; } = onDeleteAction;
        public List<string> ColumnNames { get; } = [];
        public List<string> ReferencedColumnNames { get; } = [];
    }
}

public sealed class MySqlImportExportService(string connectionString, DatabaseAccessMode accessMode) : IDatabaseImportExportService
{
    public async Task<DatabaseTransferResult> ExportAsync(
        DatabaseExportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Format != DatabaseTransferFormat.Sql)
        {
            throw new NotSupportedException($"Unsupported export format: {options.Format}");
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var lines = new List<string>();
        var tables = new List<string>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT TABLE_NAME
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var table in tables)
        {
            var createSql = await GetCreateTableSqlAsync(connection, table, cancellationToken);
            if (createSql is not null)
            {
                lines.Add($"{createSql};");
                lines.Add(string.Empty);
            }

            await using var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = $"SELECT * FROM `{table.Replace("`", "``")}`;";
            await using var dataReader = await dataCmd.ExecuteReaderAsync(cancellationToken);

            while (await dataReader.ReadAsync(cancellationToken))
            {
                var values = new string[dataReader.FieldCount];
                for (var i = 0; i < dataReader.FieldCount; i++)
                {
                    values[i] = ToSqlLiteral(dataReader.GetValue(i));
                }

                lines.Add($"INSERT INTO `{table.Replace("`", "``")}` VALUES ({string.Join(", ", values)});");
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

        await using var connection = CreateConnection();
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

    private static async Task<string?> GetCreateTableSqlAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SHOW CREATE TABLE `{tableName.Replace("`", "``")}`;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        return null;
    }

    private MySqlConnection CreateConnection()
    {
        return new MySqlConnection(connectionString);
    }

    private static List<string> SplitSqlStatements(string script)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();
        var inSingle = false;
        var inDouble = false;
        var inBacktick = false;

        for (var i = 0; i < script.Length; i++)
        {
            var ch = script[i];
            var next = i + 1 < script.Length ? script[i + 1] : '\0';

            if (!inSingle && !inDouble && !inBacktick && ch == '-' && next == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (!inSingle && !inDouble && !inBacktick && ch == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }
                i++;
                continue;
            }

            if (ch == '\'' && !inDouble && !inBacktick)
            {
                inSingle = !inSingle;
            }
            else if (ch == '"' && !inSingle && !inBacktick)
            {
                inDouble = !inDouble;
            }
            else if (ch == '`' && !inSingle && !inDouble)
            {
                inBacktick = !inBacktick;
            }

            if (ch == ';' && !inSingle && !inDouble && !inBacktick)
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
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
    }
}
