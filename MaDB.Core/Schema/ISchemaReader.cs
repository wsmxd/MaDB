namespace MaDB.Core.Schema;

public interface ISchemaReader
{
    Task<DatabaseSchema> ReadSchemaAsync(CancellationToken cancellationToken = default);
}
