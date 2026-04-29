using System.Text;
using System.Text.Json;
using MaDB.Core;
using MaDB.Core.MySql;
using MaDB.Core.PostgreSql;
using MaDB.Core.Schema;
using MaDB.Core.Sqlite;
using MaDB.Core.Transfer;

const int MaxDisplayRows = 100;

var session = new CliSession(new DatabaseProviderRegistry(
[
    new SqliteDatabaseProvider(),
    new MySqlDatabaseProvider(),
    new PostgreSqlDatabaseProvider()
]));

if (args.Length > 0)
{
    await ExecuteCommandAsync(session, string.Join(' ', args));
    return;
}

Console.WriteLine("MaDB CLI. Type `help` for commands, `exit` to quit.");
while (true)
{
    Console.Write("ma> ");
    var line = Console.ReadLine();
    if (line is null)
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    if (line.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    await ExecuteCommandAsync(session, line);
}

static async Task ExecuteCommandAsync(CliSession session, string line)
{
    try
    {
        var tokens = Tokenize(line);
        if (tokens.Count == 0)
        {
            return;
        }

        if (tokens[0].Equals("ma", StringComparison.OrdinalIgnoreCase))
        {
            tokens.RemoveAt(0);
            if (tokens.Count == 0)
            {
                WriteHelp();
                return;
            }
        }

        var command = tokens[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                WriteHelp();
                break;
            case "connect":
                await ConnectAsync(session, tokens);
                break;
            case "mode":
                await SwitchModeAsync(session, tokens);
                break;
            case "format":
                SetFormat(session, tokens);
                break;
            case "tables":
                await TablesAsync(session);
                break;
            case "schema":
                await SchemaAsync(session);
                break;
            case "query":
                await QueryAsync(session, tokens);
                break;
            case "exec":
                await ExecAsync(session, tokens);
                break;
            case "export":
                await ExportAsync(session, tokens);
                break;
            case "import":
                await ImportAsync(session, tokens);
                break;
            case "init":
                await InitAsync(session);
                break;
            case "status":
                WriteStatus(session);
                break;
            case "exit":
            case "quit":
                Environment.Exit(0);
                break;
            default:
                WriteError(session, $"Unknown command: {tokens[0]}");
                break;
        }
    }
    catch (Exception ex)
    {
        WriteError(session, ex.Message);
    }
}

static async Task ConnectAsync(CliSession session, IReadOnlyList<string> tokens)
{
    if (tokens.Count < 3)
    {
        WriteError(session, "Usage: connect <sqlite|mysql|pgsql> <target> [readonly|readwrite]");
        return;
    }

    if (!TryParseDialect(tokens[1], out var dialect))
    {
        WriteError(session, $"Unsupported dialect: {tokens[1]}");
        return;
    }

    var database = tokens[2];
    var mode = session.AccessMode;
    if (tokens.Count > 3 && !TryParseAccessMode(tokens[3], out mode))
    {
        WriteError(session, $"Unsupported mode: {tokens[3]}");
        return;
    }

    var options = session.Registry.CreateConnectionOptions(dialect, database, mode);
    session.Connect(database, options);
    await Task.CompletedTask;

    WriteMessage(session, new
    {
        ok = true,
        operation = "connect",
        dialect = session.Dialect?.ToString().ToLowerInvariant(),
        database = session.DatabaseName,
        mode = session.AccessMode.ToString().ToLowerInvariant()
    }, $"Connected to {database} ({mode}).");
}

static async Task SwitchModeAsync(CliSession session, IReadOnlyList<string> tokens)
{
    if (tokens.Count < 2)
    {
        WriteError(session, "Usage: mode readonly|readwrite");
        return;
    }

    if (!TryParseAccessMode(tokens[1], out var mode))
    {
        WriteError(session, $"Unsupported mode: {tokens[1]}");
        return;
    }

    if (!session.IsConnected)
    {
        session.AccessMode = mode;
        WriteMessage(session, new
        {
            ok = true,
            operation = "mode",
            mode = session.AccessMode.ToString().ToLowerInvariant()
        }, $"Default mode set to {mode}.");
        return;
    }

    session.ReconnectWithMode(mode);
    await Task.CompletedTask;

    WriteMessage(session, new
    {
        ok = true,
        operation = "mode",
        mode = session.AccessMode.ToString().ToLowerInvariant()
    }, $"Connection mode switched to {mode}.");
}

static void SetFormat(CliSession session, IReadOnlyList<string> tokens)
{
    if (tokens.Count < 2)
    {
        WriteError(session, "Usage: format table|json|jsonl");
        return;
    }

    if (!TryParseOutputFormat(tokens[1], out var format))
    {
        WriteError(session, $"Unsupported format: {tokens[1]}");
        return;
    }

    session.Format = format;
    WriteMessage(session, new
    {
        ok = true,
        operation = "format",
        format = session.Format.ToString().ToLowerInvariant()
    }, $"Output format set to {format}.");
}

static async Task TablesAsync(CliSession session)
{
    EnsureConnected(session);
    if (session.SchemaReader is null)
    {
        throw new InvalidOperationException("Schema reader is unavailable for current provider.");
    }

    var schema = await session.SchemaReader.ReadSchemaAsync();
    var tableRows = schema.Tables
        .Where(t => t.Type == TableType.Table)
        .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
        .Select(t => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["name"] = t.Name
        })
        .ToArray();

    var result = new QueryResult(["name"], tableRows);
    WriteQueryResult(session, result);
}

static async Task SchemaAsync(CliSession session)
{
    EnsureConnected(session);
    if (session.SchemaReader is null)
    {
        throw new InvalidOperationException("Schema reader is unavailable for current provider.");
    }

    var schema = await session.SchemaReader.ReadSchemaAsync();
    if (session.Format == OutputFormat.Table)
    {
        if (schema.Tables.Count == 0)
        {
            Console.WriteLine("No tables/views found.");
            return;
        }

        foreach (var table in schema.Tables)
        {
            Console.WriteLine($"{table.Type.ToString().ToLowerInvariant()}: {table.Name}");
            foreach (var column in table.Columns)
            {
                var nullable = column.IsNullable ? "NULL" : "NOT NULL";
                var pk = column.IsPrimaryKey ? " PK" : string.Empty;
                Console.WriteLine($"  - {column.Name} {column.DataType} {nullable}{pk}");
            }
        }
        return;
    }

    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, schema }));
}

static async Task QueryAsync(CliSession session, IReadOnlyList<string> tokens)
{
    EnsureConnected(session);

    var sql = ExtractQuerySql(tokens);
    if (string.IsNullOrWhiteSpace(sql))
    {
        WriteError(session, "Usage: query \"<sql>\"");
        return;
    }

    if (LooksLikeResultSetQuery(sql))
    {
        var result = await session.Executor!.ExecuteQueryAsync(sql);
        WriteQueryResult(session, result);
    }
    else
    {
        var affected = await session.Executor!.ExecuteNonQueryAsync(sql);
        WriteNonQueryResult(session, sql, affected);
    }
}

static async Task ExecAsync(CliSession session, IReadOnlyList<string> tokens)
{
    EnsureConnected(session);
    if (tokens.Count < 2)
    {
        WriteError(session, "Usage: exec \"<script-file.sql>\"");
        return;
    }

    var scriptPath = tokens[1];
    if (!File.Exists(scriptPath))
    {
        WriteError(session, $"Script file not found: {scriptPath}");
        return;
    }

    var script = await File.ReadAllTextAsync(scriptPath);
    var statements = SplitSqlStatements(script);
    var executed = 0;

    foreach (var statement in statements)
    {
        if (LooksLikeResultSetQuery(statement))
        {
            var result = await session.Executor!.ExecuteQueryAsync(statement);
            WriteQueryResult(session, result);
        }
        else
        {
            var affected = await session.Executor!.ExecuteNonQueryAsync(statement);
            WriteNonQueryResult(session, statement, affected);
        }

        executed++;
    }

    WriteMessage(session, new
    {
        ok = true,
        operation = "exec",
        file = scriptPath,
        statements = executed
    }, $"Script executed: {scriptPath} (statements: {executed})");
}

static async Task ExportAsync(CliSession session, IReadOnlyList<string> tokens)
{
    EnsureConnected(session);
    if (session.TransferService is null)
    {
        throw new InvalidOperationException("Import/export service is unavailable for current provider.");
    }

    if (tokens.Count < 2)
    {
        WriteError(session, "Usage: export \"<output-file.sql>\"");
        return;
    }

    var outputPath = tokens[1];
    var result = await session.TransferService.ExportAsync(new DatabaseExportOptions(outputPath));
    WriteMessage(session, new
    {
        ok = true,
        operation = "export",
        path = result.Path,
        statements = result.StatementsProcessed
    }, $"Exported to {result.Path} (statements: {result.StatementsProcessed})");
}

static async Task ImportAsync(CliSession session, IReadOnlyList<string> tokens)
{
    EnsureConnected(session);
    if (session.TransferService is null)
    {
        throw new InvalidOperationException("Import/export service is unavailable for current provider.");
    }

    if (tokens.Count < 2)
    {
        WriteError(session, "Usage: import \"<input-file.sql>\"");
        return;
    }

    var inputPath = tokens[1];
    var result = await session.TransferService.ImportAsync(new DatabaseImportOptions(inputPath));
    WriteMessage(session, new
    {
        ok = true,
        operation = "import",
        path = result.Path,
        statements = result.StatementsProcessed
    }, $"Imported from {result.Path} (statements: {result.StatementsProcessed})");
}

static async Task InitAsync(CliSession session)
{
    EnsureConnected(session);

    const string createTableSql = """
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            email TEXT NOT NULL UNIQUE
        );
        """;

    await session.Executor!.ExecuteNonQueryAsync(createTableSql);

    const string seedSql = """
        INSERT OR IGNORE INTO users(name, email)
        VALUES
            ('Alice', 'alice@example.com'),
            ('Bob', 'bob@example.com');
        """;

    var affected = await session.Executor.ExecuteNonQueryAsync(seedSql);
    WriteNonQueryResult(session, "init", affected);
}

static void WriteStatus(CliSession session)
{
    var payload = new
    {
        ok = true,
        connected = session.IsConnected,
        dialect = session.Dialect?.ToString().ToLowerInvariant(),
        database = session.DatabaseName,
        mode = session.AccessMode.ToString().ToLowerInvariant(),
        format = session.Format.ToString().ToLowerInvariant()
    };

    WriteMessage(session, payload,
        session.IsConnected
            ? $"Connected: {session.Dialect} {session.DatabaseName} ({session.AccessMode})"
            : $"Not connected. Default mode: {session.AccessMode}, format: {session.Format}");
}

static void WriteHelp()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  ma connect <sqlite|mysql|pgsql> <target> [readonly|readwrite]");
    Console.WriteLine("  ma mode readonly|readwrite");
    Console.WriteLine("  ma query \"<sql>\"");
    Console.WriteLine("  ma exec \"<script-file.sql>\"");
    Console.WriteLine("  ma schema");
    Console.WriteLine("  ma export \"<output-file.sql>\"");
    Console.WriteLine("  ma import \"<input-file.sql>\"");
    Console.WriteLine("  ma tables");
    Console.WriteLine("  ma init");
    Console.WriteLine("  ma status");
    Console.WriteLine("  ma format table|json|jsonl");
    Console.WriteLine("  ma exit");
}

static void WriteQueryResult(CliSession session, QueryResult result)
{
    var limitedRows = result.Rows.Take(MaxDisplayRows).ToArray();
    var truncated = result.Rows.Count > MaxDisplayRows;

    switch (session.Format)
    {
        case OutputFormat.Table:
            if (result.Rows.Count == 0)
            {
                Console.WriteLine("No rows found.");
                return;
            }

            Console.WriteLine(string.Join(" | ", result.Columns));
            foreach (var row in limitedRows)
            {
                Console.WriteLine(string.Join(" | ", result.Columns.Select(col => row[col])));
            }
            if (truncated)
            {
                Console.WriteLine($"... truncated: showing first {MaxDisplayRows} rows out of {result.Rows.Count}.");
            }
            break;
        case OutputFormat.Json:
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                rowCount = result.Rows.Count,
                displayedRowCount = limitedRows.Length,
                truncated,
                columns = result.Columns,
                rows = limitedRows
            }));
            break;
        case OutputFormat.Jsonl:
            foreach (var row in limitedRows)
            {
                Console.WriteLine(JsonSerializer.Serialize(row));
            }
            if (truncated)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    truncated = true,
                    rowCount = result.Rows.Count,
                    displayedRowCount = limitedRows.Length
                }));
            }
            break;
    }
}

static void WriteNonQueryResult(CliSession session, string sql, int affectedRows)
{
    var operation = FirstWord(sql).ToLowerInvariant();
    WriteMessage(session, new
    {
        ok = true,
        operation,
        affectedRows
    }, $"OK ({operation}), affected rows: {affectedRows}");
}

static void WriteMessage(CliSession session, object payload, string tableText)
{
    if (session.Format == OutputFormat.Table)
    {
        Console.WriteLine(tableText);
        return;
    }

    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static void WriteError(CliSession session, string message)
{
    if (session.Format == OutputFormat.Table)
    {
        Console.Error.WriteLine($"Error: {message}");
        return;
    }

    Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        ok = false,
        error = message
    }));
}

static void EnsureConnected(CliSession session)
{
    if (!session.IsConnected || session.Executor is null)
    {
        throw new InvalidOperationException("Not connected. Use: connect sqlite <db-file> [readonly|readwrite]");
    }
}

static string ExtractQuerySql(IReadOnlyList<string> tokens)
{
    if (tokens.Count < 2)
    {
        return string.Empty;
    }

    return string.Join(' ', tokens.Skip(1));
}

static bool LooksLikeResultSetQuery(string sql)
{
    var first = FirstWord(sql).ToLowerInvariant();
    return first is "select" or "with" or "pragma" or "explain";
}

static List<string> SplitSqlStatements(string script)
{
    var statements = new List<string>();
    var sb = new StringBuilder();
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

static string FirstWord(string text)
{
    var span = text.AsSpan().TrimStart();
    for (var i = 0; i < span.Length; i++)
    {
        if (char.IsWhiteSpace(span[i]))
        {
            return span[..i].ToString();
        }
    }

    return span.ToString();
}

static List<string> Tokenize(string input)
{
    var result = new List<string>();
    var sb = new StringBuilder();
    var inQuotes = false;

    for (var i = 0; i < input.Length; i++)
    {
        var ch = input[i];
        if (ch == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }

        if (char.IsWhiteSpace(ch) && !inQuotes)
        {
            if (sb.Length > 0)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            continue;
        }

        sb.Append(ch);
    }

    if (sb.Length > 0)
    {
        result.Add(sb.ToString());
    }

    return result;
}

static bool TryParseDialect(string input, out DatabaseDialect dialect)
{
    dialect = input.ToLowerInvariant() switch
    {
        "sqlite" => DatabaseDialect.Sqlite,
        "mysql" => DatabaseDialect.MySql,
        "pgsql" or "postgres" or "postgresql" => DatabaseDialect.PostgreSql,
        _ => default
    };

    return input.Equals("sqlite", StringComparison.OrdinalIgnoreCase)
        || input.Equals("mysql", StringComparison.OrdinalIgnoreCase)
        || input.Equals("pgsql", StringComparison.OrdinalIgnoreCase)
        || input.Equals("postgres", StringComparison.OrdinalIgnoreCase)
        || input.Equals("postgresql", StringComparison.OrdinalIgnoreCase);
}

static bool TryParseAccessMode(string input, out DatabaseAccessMode mode)
{
    mode = input.ToLowerInvariant() switch
    {
        "readonly" or "ro" => DatabaseAccessMode.ReadOnly,
        "readwrite" or "rw" => DatabaseAccessMode.ReadWrite,
        _ => default
    };

    return input.Equals("readonly", StringComparison.OrdinalIgnoreCase)
        || input.Equals("ro", StringComparison.OrdinalIgnoreCase)
        || input.Equals("readwrite", StringComparison.OrdinalIgnoreCase)
        || input.Equals("rw", StringComparison.OrdinalIgnoreCase);
}

static bool TryParseOutputFormat(string input, out OutputFormat format)
{
    format = input.ToLowerInvariant() switch
    {
        "table" => OutputFormat.Table,
        "json" => OutputFormat.Json,
        "jsonl" => OutputFormat.Jsonl,
        _ => default
    };

    return input.Equals("table", StringComparison.OrdinalIgnoreCase)
        || input.Equals("json", StringComparison.OrdinalIgnoreCase)
        || input.Equals("jsonl", StringComparison.OrdinalIgnoreCase);
}

enum OutputFormat
{
    Table = 1,
    Json = 2,
    Jsonl = 3
}

sealed class CliSession(DatabaseProviderRegistry registry)
{
    public DatabaseProviderRegistry Registry => registry;
    public IQueryExecutor? Executor { get; private set; }
    public ISchemaReader? SchemaReader { get; private set; }
    public IDatabaseImportExportService? TransferService { get; private set; }
    public DatabaseDialect? Dialect { get; private set; }
    public string? DatabaseName { get; private set; }
    public DatabaseConnectionOptions? Options { get; private set; }
    public DatabaseAccessMode AccessMode { get; set; } = DatabaseAccessMode.ReadWrite;
    public OutputFormat Format { get; set; } = OutputFormat.Table;
    public bool IsConnected => Executor is not null;

    public void Connect(string databaseName, DatabaseConnectionOptions options)
    {
        Dialect = options.Dialect;
        DatabaseName = databaseName;
        Options = options;
        AccessMode = options.AccessMode;
        Executor = registry.CreateExecutor(options);
        SchemaReader = registry.CreateSchemaReader(options);
        TransferService = registry.CreateImportExportService(options);
    }

    public void ReconnectWithMode(DatabaseAccessMode mode)
    {
        if (Dialect is null || DatabaseName is null || Options is null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var options = Options with { AccessMode = mode };
        Connect(DatabaseName, options);
    }
}
