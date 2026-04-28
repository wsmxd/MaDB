using MaDB.Core.Schema;
using MaDB.Core.Transfer;

namespace MaDB.Core;

public interface IDatabaseProvider
{
    DatabaseDialect Dialect { get; }

    DatabaseConnectionOptions CreateConnectionOptions(string target, DatabaseAccessMode accessMode);

    IQueryExecutor CreateQueryExecutor(DatabaseConnectionOptions options);

    ISchemaReader CreateSchemaReader(DatabaseConnectionOptions options);

    IDatabaseImportExportService CreateImportExportService(DatabaseConnectionOptions options);
}
