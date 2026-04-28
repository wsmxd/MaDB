namespace MaDB.Core;

public interface IDatabaseProvider
{
    DatabaseDialect Dialect { get; }

    IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options);
}
