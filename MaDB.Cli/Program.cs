using System.Text;
using System.Text.Json;
using MaDB.Core;
using MaDB.Core.Sqlite;

var session = new CliSession(new DatabaseProviderRegistry([new SqliteDatabaseProvider()]));

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
            case "query":
                await QueryAsync(session, tokens);
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
        WriteError(session, "Usage: connect sqlite <db-file> [readonly|readwrite]");
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

    var connectionString = dialect switch
    {
        DatabaseDialect.Sqlite => $"Data Source={database}",
        _ => throw new NotSupportedException($"Unsupported dialect: {dialect}")
    };

    session.Connect(dialect, database, connectionString, mode);
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
    var result = await session.Executor!.ExecuteQueryAsync(
        """
        SELECT name
        FROM sqlite_master
        WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
        ORDER BY name;
        """);

    WriteQueryResult(session, result);
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
    Console.WriteLine("  ma connect sqlite <db-file> [readonly|readwrite]");
    Console.WriteLine("  ma mode readonly|readwrite");
    Console.WriteLine("  ma query \"<sql>\"");
    Console.WriteLine("  ma tables");
    Console.WriteLine("  ma init");
    Console.WriteLine("  ma status");
    Console.WriteLine("  ma format table|json|jsonl");
    Console.WriteLine("  ma exit");
}

static void WriteQueryResult(CliSession session, QueryResult result)
{
    switch (session.Format)
    {
        case OutputFormat.Table:
            if (result.Rows.Count == 0)
            {
                Console.WriteLine("No rows found.");
                return;
            }

            Console.WriteLine(string.Join(" | ", result.Columns));
            foreach (var row in result.Rows)
            {
                Console.WriteLine(string.Join(" | ", result.Columns.Select(col => row[col])));
            }
            break;
        case OutputFormat.Json:
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                rowCount = result.Rows.Count,
                columns = result.Columns,
                rows = result.Rows
            }));
            break;
        case OutputFormat.Jsonl:
            foreach (var row in result.Rows)
            {
                Console.WriteLine(JsonSerializer.Serialize(row));
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
        _ => default
    };

    return input.Equals("sqlite", StringComparison.OrdinalIgnoreCase);
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
    public IQueryExecutor? Executor { get; private set; }
    public DatabaseDialect? Dialect { get; private set; }
    public string? DatabaseName { get; private set; }
    public string? ConnectionString { get; private set; }
    public DatabaseAccessMode AccessMode { get; set; } = DatabaseAccessMode.ReadWrite;
    public OutputFormat Format { get; set; } = OutputFormat.Table;
    public bool IsConnected => Executor is not null;

    public void Connect(
        DatabaseDialect dialect,
        string databaseName,
        string connectionString,
        DatabaseAccessMode mode)
    {
        Dialect = dialect;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
        AccessMode = mode;
        Executor = registry.CreateExecutor(new DatabaseConnectionOptions(dialect, connectionString, mode));
    }

    public void ReconnectWithMode(DatabaseAccessMode mode)
    {
        if (Dialect is null || ConnectionString is null || DatabaseName is null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        Connect(Dialect.Value, DatabaseName, ConnectionString, mode);
    }
}
