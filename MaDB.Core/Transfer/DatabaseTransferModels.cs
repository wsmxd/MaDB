namespace MaDB.Core.Transfer;

public enum DatabaseTransferFormat
{
    Sql = 1
}

public sealed record DatabaseExportOptions(
    string OutputPath,
    DatabaseTransferFormat Format = DatabaseTransferFormat.Sql);

public sealed record DatabaseImportOptions(
    string InputPath,
    DatabaseTransferFormat Format = DatabaseTransferFormat.Sql);

public sealed record DatabaseTransferResult(
    bool Success,
    string Path,
    int StatementsProcessed);
