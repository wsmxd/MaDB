using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Npgsql;
using MaDB.Core.Schema;
using MaDB.Core.Transfer;

namespace MaDB.Core.PostgreSql;

public sealed class PostgreSqlDatabaseProvider : IDatabaseProvider
{
    public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;

    public DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("PostgreSQL connection string or target is required.", nameof(target));
        }

        var connectionString = target.Contains('=')
            ? target
            : BuildConnectionString(target);

        return new DatabaseConnectionOptions(DatabaseDialect.PostgreSql, connectionString, accessMode);
    }

    public IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.PostgreSql)
        {
            throw new ArgumentException("PostgreSQL provider can only handle PostgreSQL options.", nameof(options));
        }

        return new PostgresQueryExecutor(options.ConnectionString, options.AccessMode);
    }

    public ISchemaReader CreateSchemaReader(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.PostgreSql)
        {
            throw new ArgumentException("PostgreSQL provider can only handle PostgreSQL options.", nameof(options));
        }

        return new PostgresSchemaReader(options.ConnectionString, options.AccessMode);
    }

    public IDatabaseImportExportService CreateImportExportService(DatabaseConnectionOptions options)
    {
        if (options.Dialect != DatabaseDialect.PostgreSql)
        {
            throw new ArgumentException("PostgreSQL provider can only handle PostgreSQL options.", nameof(options));
        }

        return new PostgresImportExportService(options.ConnectionString, options.AccessMode);
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

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Username = "postgres"
        };

        if (port.HasValue)
        {
            builder.Port = port.Value;
        }

        return builder.ConnectionString;
    }
}

public sealed class PostgresQueryExecutor(string connectionString, DatabaseAccessMode accessMode) : IQueryExecutor
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
        return new PostgresQueryTransaction(connection, transaction);
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

    private NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(connectionString);
    }

    private void EnsureWritable()
    {
        if (accessMode == DatabaseAccessMode.ReadOnly)
        {
            throw new InvalidOperationException("Current connection is read-only.");
        }
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
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

public sealed class PostgresQueryTransaction : IQueryTransaction
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _committed;
    private bool _rollbacked;

    internal PostgresQueryTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
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

public sealed class PostgresSchemaReader(string connectionString, DatabaseAccessMode accessMode) : ISchemaReader
{
    public async Task<DatabaseSchema> ReadSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var tableEntries = new List<(string Name, TableType Type, string? Comment)>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT table_name, table_type
                FROM information_schema.tables
                WHERE table_schema = 'public'
                ORDER BY table_type, table_name;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var tableType = reader.GetString(1);
                var type = tableType == "VIEW" ? TableType.View : TableType.Table;
                tableEntries.Add((name, type, null));
            }
        }

        var tables = new List<TableSchema>();
        foreach (var entry in tableEntries)
        {
            var columns = await ReadColumnsAsync(connection, entry.Name, cancellationToken);
            var indexes = await ReadIndexesAsync(connection, entry.Name, cancellationToken);
            var foreignKeys = await ReadForeignKeysAsync(connection, entry.Name, cancellationToken);
            tables.Add(new TableSchema(entry.Name, entry.Type, columns, indexes, foreignKeys, null));
        }

        return new DatabaseSchema(tables);
    }

    private static async Task<IReadOnlyList<ColumnSchema>> ReadColumnsAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                c.column_name,
                c.data_type,
                c.udt_name,
                c.is_nullable,
                c.column_default,
                c.ordinal_position,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale,
                CASE WHEN p.column_name IS NOT NULL THEN true ELSE false END AS is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                WHERE tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'
            ) p ON c.column_name = p.column_name
            WHERE c.table_schema = 'public' AND c.table_name = @table
            ORDER BY c.ordinal_position;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var udtName = reader.GetString(2);
            var isNullable = reader.GetString(3) == "YES";
            var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
            var ordinal = reader.GetInt32(5) - 1;
            var maxLength = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
            var precision = reader.IsDBNull(7) ? (int?)null : (int)reader.GetInt64(7);
            var scale = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
            var isPrimaryKey = reader.GetBoolean(9);

            var displayType = dataType == "USER-DEFINED" || dataType == "ARRAY"
                ? udtName
                : dataType;

            var isAutoIncrement = defaultValue is not null
                && defaultValue.Contains("nextval", StringComparison.OrdinalIgnoreCase);

            columns.Add(new ColumnSchema(
                name,
                displayType,
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
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var indexMap = new Dictionary<string, (bool IsUnique, bool IsPrimary, List<IndexColumnSchema> Columns)>(StringComparer.OrdinalIgnoreCase);
        var indexOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                i.relname AS index_name,
                idx.indisunique AS is_unique,
                idx.indisprimary AS is_primary,
                a.attname AS column_name
            FROM pg_index idx
            JOIN pg_class c ON idx.indrelid = c.oid
            JOIN pg_class i ON idx.indexrelid = i.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(idx.indkey)
            WHERE c.relname = @table AND n.nspname = 'public'
            ORDER BY i.relname, a.attnum;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var indexName = reader.GetString(0);
            var isUnique = reader.GetBoolean(1);
            var isPrimary = reader.GetBoolean(2);
            var columnName = reader.GetString(3);

            if (!indexMap.TryGetValue(indexName, out var entry))
            {
                entry = (isUnique, isPrimary, []);
                indexMap[indexName] = entry;
                indexOrders[indexName] = 0;
            }

            entry.Columns.Add(new IndexColumnSchema(columnName, indexOrders[indexName], false));
            indexOrders[indexName]++;
        }

        return indexMap.Select(kv => new IndexSchema(
            kv.Key,
            kv.Value.IsUnique,
            kv.Value.IsPrimary,
            kv.Value.Columns)).ToList();
    }

    private static async Task<IReadOnlyList<ForeignKeySchema>> ReadForeignKeysAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var fkMap = new Dictionary<string, ForeignKeyEntry>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                tc.constraint_name,
                kcu.column_name,
                ccu.table_name AS referenced_table,
                ccu.column_name AS referenced_column,
                rc.update_rule,
                rc.delete_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.referential_constraints rc
                ON tc.constraint_name = rc.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON tc.constraint_name = ccu.constraint_name
            WHERE tc.table_schema = 'public'
                AND tc.table_name = @table
                AND tc.constraint_type = 'FOREIGN KEY'
            ORDER BY tc.constraint_name, kcu.ordinal_position;
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

    private NpgsqlConnection CreateConnection()
    {
        _ = accessMode;
        return new NpgsqlConnection(connectionString);
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

public sealed class PostgresImportExportService(string connectionString, DatabaseAccessMode accessMode) : IDatabaseImportExportService
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
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
                ORDER BY table_name;
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
            dataCmd.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\";";
            await using var dataReader = await dataCmd.ExecuteReaderAsync(cancellationToken);

            var columnNames = Enumerable.Range(0, dataReader.FieldCount)
                .Select(dataReader.GetName)
                .Select(n => $"\"{n.Replace("\"", "\"\"")}\"")
                .ToArray();

            while (await dataReader.ReadAsync(cancellationToken))
            {
                var values = new string[dataReader.FieldCount];
                for (var i = 0; i < dataReader.FieldCount; i++)
                {
                    values[i] = ToSqlLiteral(dataReader.GetValue(i));
                }

                lines.Add($"INSERT INTO \"{table.Replace("\"", "\"\"")}\" ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", values)});");
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
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sequences = await GetSequenceDefinitionsAsync(connection, tableName, cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                'CREATE TABLE ' || quote_ident(@table) || ' (' ||
                string_agg(
                    quote_ident(a.attname) || ' ' ||
                    pg_catalog.format_type(a.atttypid, a.atttypmod) ||
                    CASE WHEN a.attnotnull THEN ' NOT NULL' ELSE '' END,
                    ', '
                    ORDER BY a.attnum
                ) || ')'
            FROM pg_attribute a
            JOIN pg_class c ON a.attrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE c.relname = @table
                AND n.nspname = 'public'
                AND a.attnum > 0
                AND NOT a.attisdropped
            GROUP BY c.relname;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var createTableSql = result as string;
        if (createTableSql is null)
        {
            return null;
        }

        if (sequences.Count == 0)
        {
            return createTableSql;
        }

        var sb = new StringBuilder();
        foreach (var seq in sequences)
        {
            sb.AppendLine($"CREATE SEQUENCE IF NOT EXISTS {seq};");
        }

        sb.Append(createTableSql);
        sb.AppendLine(";");

        await using var defaultCmd = connection.CreateCommand();
        defaultCmd.CommandText = """
            SELECT
                quote_ident(a.attname),
                pg_get_expr(d.adbin, d.adrelid)
            FROM pg_attribute a
            JOIN pg_class c ON a.attrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            JOIN pg_attrdef d ON a.attrelid = d.adrelid AND a.attnum = d.adnum
            WHERE c.relname = @table
                AND n.nspname = 'public'
                AND a.attnum > 0
                AND NOT a.attisdropped
                AND d.adbin IS NOT NULL;
            """;
        defaultCmd.Parameters.AddWithValue("@table", tableName);

        await using var defaultReader = await defaultCmd.ExecuteReaderAsync(cancellationToken);
        while (await defaultReader.ReadAsync(cancellationToken))
        {
            var columnName = defaultReader.GetString(0);
            var defaultValue = defaultReader.GetString(1);
            sb.AppendLine($"ALTER TABLE {tableName} ALTER COLUMN {columnName} SET DEFAULT {defaultValue};");
        }

        return sb.ToString().TrimEnd().TrimEnd(';');
    }

    private static async Task<List<string>> GetSequenceDefinitionsAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sequences = new List<string>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                pg_get_serial_sequence(quote_ident(@table), a.attname)
            FROM pg_attribute a
            JOIN pg_class c ON a.attrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE c.relname = @table
                AND n.nspname = 'public'
                AND a.attnum > 0
                AND NOT a.attisdropped
                AND pg_get_serial_sequence(quote_ident(@table), a.attname) IS NOT NULL;
            """;
        cmd.Parameters.AddWithValue("@table", tableName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                sequences.Add(reader.GetString(0));
            }
        }

        return sequences;
    }

    private NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(connectionString);
    }

    private static List<string> SplitSqlStatements(string script)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();
        var inSingle = false;
        var inDouble = false;
        var inDollar = false;
        var dollarTag = string.Empty;

        for (var i = 0; i < script.Length; i++)
        {
            var ch = script[i];
            var next = i + 1 < script.Length ? script[i + 1] : '\0';

            if (!inSingle && !inDouble && !inDollar && ch == '-' && next == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    i++;
                }
                continue;
            }

            if (!inSingle && !inDouble && !inDollar && ch == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }
                i++;
                continue;
            }

            if (!inDollar && ch == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (!inDollar && ch == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }
            else if (!inSingle && !inDouble && ch == '$' && next == '$')
            {
                if (!inDollar)
                {
                    inDollar = true;
                }
                else
                {
                    inDollar = false;
                }
            }

            if (ch == ';' && !inSingle && !inDouble && !inDollar)
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
            bool b => b ? "TRUE" : "FALSE",
            byte[] bytes => $"'\\x{Convert.ToHexString(bytes)}'",
            DateTime dt => $"'{dt:O}'",
            DateTimeOffset dto => $"'{dto:O}'",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"
        };
    }
}
