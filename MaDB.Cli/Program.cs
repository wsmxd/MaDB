using MaDB.Core;
using MaDB.Core.Sqlite;

var sqliteFile = args.Length > 0 ? args[0] : "madb.sqlite.db";
var command = args.Length > 1 ? args[1].ToLowerInvariant() : "demo";

var options = new DatabaseConnectionOptions(
    DatabaseDialect.Sqlite,
    $"Data Source={sqliteFile}");

var registry = new DatabaseProviderRegistry(
    [new SqliteDatabaseProvider()]);

var executor = registry.CreateExecutor(options);

switch (command)
{
    case "init":
        await InitAsync(executor);
        Console.WriteLine($"Initialized database: {sqliteFile}");
        break;
    case "query":
        await QueryUsersAsync(executor);
        break;
    case "demo":
        await InitAsync(executor);
        await QueryUsersAsync(executor);
        break;
    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project MaDB.Cli [db-file] [init|query|demo]");
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project MaDB.Cli");
        Console.WriteLine("  dotnet run --project MaDB.Cli test.db init");
        Console.WriteLine("  dotnet run --project MaDB.Cli test.db query");
        break;
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

static async Task QueryUsersAsync(IQueryExecutor executor)
{
    var result = await executor.ExecuteQueryAsync(
        "SELECT id, name, email FROM users ORDER BY id;");

    if (result.Rows.Count == 0)
    {
        Console.WriteLine("No rows found.");
        return;
    }

    Console.WriteLine(string.Join(" | ", result.Columns));
    foreach (var row in result.Rows)
    {
        Console.WriteLine(
            string.Join(" | ", result.Columns.Select(col => row[col])));
    }
}
