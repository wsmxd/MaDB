namespace MaDB.Core;

public interface IDatabaseProvider
{
    DatabaseDialect Dialect { get; }

    DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode);

    IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options);
}
