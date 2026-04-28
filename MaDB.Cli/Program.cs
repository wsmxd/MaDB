using System.Text.Json;
using MaDB.Core;
using MaDB.Core.Sqlite;

var format = OutputFormat.Table;
var positionalArgs = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    var current = args[i];
    if (string.Equals(current, "--format", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 >= args.Length || !TryParseOutputFormat(args[i + 1], out format))
        {
            WriteUsage("Invalid or missing value for --format. Supported: table, json, jsonl.");
            return;
        }
        i++;
        continue;
    }

    positionalArgs.Add(current);
}

var sqliteFile = positionalArgs.Count > 0 ? positionalArgs[0] : "madb.sqlite.db";
var command = positionalArgs.Count > 1 ? positionalArgs[1].ToLowerInvariant() : "demo";

var options = new DatabaseConnectionOptions(
    DatabaseDialect.Sqlite,
    $"Data Source={sqliteFile}");

var registry = new DatabaseProviderRegistry(
    [new SqliteDatabaseProvider()]);

var executor = registry.CreateExecutor(options);

try
{
    switch (command)
    {
        case "init":
            await InitAsync(executor);
            WriteInitResult(sqliteFile, format);
            break;
        case "query":
            await QueryUsersAsync(executor, format);
            break;
        case "demo":
            await InitAsync(executor);
            WriteInitResult(sqliteFile, format);
            await QueryUsersAsync(executor, format);
            break;
        default:
            WriteUsage($"Unknown command: {command}");
            break;
    }
}
catch (Exception ex)
{
    if (format == OutputFormat.Table)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
    else
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            ok = false,
            error = ex.Message
        }));
    }
}

static async Task InitAsync(IQueryExecutor executor)
{
    const string createTableSql = """
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            email TEXT NOT NULL UNIQUE
        );
        """;

    await executor.ExecuteNonQueryAsync(createTableSql);

    const string seedSql = """
        INSERT OR IGNORE INTO users(name, email)
        VALUES
            ('Alice', 'alice@example.com'),
            ('Bob', 'bob@example.com');
        """;

    await executor.ExecuteNonQueryAsync(seedSql);
}

static async Task QueryUsersAsync(IQueryExecutor executor, OutputFormat format)
{
    var result = await executor.ExecuteQueryAsync(
        "SELECT id, name, email FROM users ORDER BY id;");

    switch (format)
    {
        case OutputFormat.Table:
            WriteTable(result);
            break;
        case OutputFormat.Json:
            WriteJson(result);
            break;
        case OutputFormat.Jsonl:
            WriteJsonl(result);
            break;
    }
}

static void WriteUsage(string reason)
{
    Console.Error.WriteLine(reason);
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project MaDB.Cli [db-file] [init|query|demo] [--format table|json|jsonl]");
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet run --project MaDB.Cli");
    Console.Error.WriteLine("  dotnet run --project MaDB.Cli test.db init --format json");
    Console.Error.WriteLine("  dotnet run --project MaDB.Cli test.db query --format jsonl");
}

static void WriteInitResult(string sqliteFile, OutputFormat format)
{
    if (format == OutputFormat.Table)
    {
        Console.WriteLine($"Initialized database: {sqliteFile}");
        return;
    }

    var payload = new
    {
        ok = true,
        operation = "init",
        database = sqliteFile
    };
    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static void WriteTable(QueryResult result)
{
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
}

static void WriteJson(QueryResult result)
{
    var payload = new
    {
        ok = true,
        rowCount = result.Rows.Count,
        columns = result.Columns,
        rows = result.Rows
    };
    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static void WriteJsonl(QueryResult result)
{
    foreach (var row in result.Rows)
    {
        Console.WriteLine(JsonSerializer.Serialize(row));
    }
}

static bool TryParseOutputFormat(string value, out OutputFormat format)
{
    format = value.ToLowerInvariant() switch
    {
        "table" => OutputFormat.Table,
        "json" => OutputFormat.Json,
        "jsonl" => OutputFormat.Jsonl,
        _ => OutputFormat.Table
    };

    return value is "table" or "json" or "jsonl";
}

enum OutputFormat
{
    Table,
    Json,
    Jsonl
}
