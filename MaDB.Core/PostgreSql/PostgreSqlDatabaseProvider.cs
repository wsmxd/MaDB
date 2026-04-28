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
            throw new ArgumentException("PostgreSQL target is required.", nameof(target));
        }

        return new DatabaseConnectionOptions(DatabaseDialect.PostgreSql, target, accessMode);
    }

    public IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options)
    {
        throw new NotSupportedException(
            "PostgreSQL provider skeleton is ready, but query executor is not implemented yet.");
    }

    public ISchemaReader CreateSchemaReader(DatabaseConnectionOptions options)
    {
        throw new NotSupportedException(
            "PostgreSQL provider skeleton is ready, but schema reader is not implemented yet.");
    }

    public IDatabaseImportExportService CreateImportExportService(DatabaseConnectionOptions options)
    {
        throw new NotSupportedException(
            "PostgreSQL provider skeleton is ready, but import/export service is not implemented yet.");
    }
}
