using System.Collections.Concurrent;
using System.Data.Common;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     The data sources the provider creates for a factory and connection string: one per pair, since drivers with their own
///     data source pool per data source and one per context would mean a pool per context. A singleton of EF's
///     internal service provider, which owns it: the data sources, their pools and the connection strings they hold are
///     released when that provider is disposed. Data sources the application passes in are the application's.
/// </summary>
internal sealed class AdoNetDataSources : IDisposable
{
    // Lazy keeps a lost race from creating, and leaking, a second data source.
    private readonly ConcurrentDictionary<(DbProviderFactory Factory, string ConnectionString), Lazy<DbDataSource>> _dataSources = new();
    private bool _disposed;

    public DbDataSource GetOrCreate(DbProviderFactory factory, string connectionString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _dataSources.GetOrAdd(
                (factory, connectionString),
                static key => new Lazy<DbDataSource>(() => key.Factory.CreateDataSource(key.ConnectionString)))
            .Value;
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var dataSource in _dataSources.Values.Where(d => d.IsValueCreated))
        {
            dataSource.Value.Dispose();
        }

        _dataSources.Clear();
    }
}
