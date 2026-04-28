namespace MaDB.Core;

public sealed class DatabaseProviderRegistry
{
    private readonly IReadOnlyDictionary<DatabaseDialect, IDatabaseProvider> _providers;

    public DatabaseProviderRegistry(IEnumerable<IDatabaseProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Dialect);
    }

    public IQueryExecutor CreateExecutor(DatabaseConnectionOptions options)
    {
        if (!_providers.TryGetValue(options.Dialect, out var provider))
        {
            throw new NotSupportedException($"Unsupported database dialect: {options.Dialect}");
        }

        return provider.CreateQueryExecutor(options);
    }
}
