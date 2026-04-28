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
        var provider = GetProvider(options.Dialect);
        return provider.CreateQueryExecutor(options);
    }

    public DatabaseConnectionOptions CreateConnectionOptions(
        DatabaseDialect dialect,
        string target,
        DatabaseAccessMode accessMode)
    {
        var provider = GetProvider(dialect);
        return provider.CreateConnectionOptions(target, accessMode);
    }

    private IDatabaseProvider GetProvider(DatabaseDialect dialect)
    {
        if (!_providers.TryGetValue(dialect, out var provider))
        {
            throw new NotSupportedException($"Unsupported database dialect: {dialect}");
        }

        return provider;
    }
}
