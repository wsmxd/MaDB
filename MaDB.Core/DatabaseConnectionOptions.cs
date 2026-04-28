namespace MaDB.Core;

public sealed record DatabaseConnectionOptions(
    DatabaseDialect Dialect,
    string ConnectionString);
