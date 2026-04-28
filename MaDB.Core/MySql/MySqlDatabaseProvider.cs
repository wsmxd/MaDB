namespace MaDB.Core.MySql;

public sealed class MySqlDatabaseProvider : IDatabaseProvider
{
    public DatabaseDialect Dialect => DatabaseDialect.MySql;

    public DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("MySQL target is required.", nameof(target));
        }

        return new DatabaseConnectionOptions(DatabaseDialect.MySql, target, accessMode);
    }

    public IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options)
    {
        throw new NotSupportedException(
            "MySQL provider skeleton is ready, but query executor is not implemented yet.");
    }
}
