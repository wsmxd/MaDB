namespace MaDB.Core.Transfer;

public interface IDatabaseImportExportService
{
    Task<DatabaseTransferResult> ExportAsync(
        DatabaseExportOptions options,
        CancellationToken cancellationToken = default);

    Task<DatabaseTransferResult> ImportAsync(
        DatabaseImportOptions options,
        CancellationToken cancellationToken = default);
}
