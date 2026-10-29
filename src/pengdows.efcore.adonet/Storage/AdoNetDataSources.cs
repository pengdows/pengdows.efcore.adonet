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

    // A dialect's required connection string options added to a raw connection string, computed once per (factory, raw
    // string) pair: a builder-based parse that a new DbContext would otherwise repeat on every CreateDbConnection call
    // for the same connection string.
    private readonly ConcurrentDictionary<(DbProviderFactory Factory, string ConnectionString), string> _augmented = new();
    private bool _disposed;

    public DbDataSource GetOrCreate(DbProviderFactory factory, string connectionString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _dataSources.GetOrAdd(
                (factory, connectionString),
                static key => new Lazy<DbDataSource>(() => key.Factory.CreateDataSource(key.ConnectionString)))
            .Value;
    }

    /// <summary>
    ///     <see cref="GetOrCreate(DbProviderFactory,string)" />, with <paramref name="connectionString" /> first passed through
    ///     <paramref name="withOptions" /> (a dialect's connection string option augmentation) the first time this exact raw
    ///     string is seen for this factory, and the result reused after.
    /// </summary>
    public DbDataSource GetOrCreate(DbProviderFactory factory, string connectionString, Func<string, string> withOptions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var augmented = _augmented.GetOrAdd((factory, connectionString), key => withOptions(key.ConnectionString));
        return GetOrCreate(factory, augmented);
    }

    /// <summary>
    ///     Disposes the data source for this pair, if one was made, closing its pool: a database about to be dropped must not
    ///     be left with pooled connections that would be handed out after it is gone.
    /// </summary>
    /// <returns>Whether there was one: connections it made are then unusable.</returns>
    public bool Evict(DbProviderFactory factory, string connectionString)
    {
        if (!_dataSources.TryRemove((factory, connectionString), out var dataSource) || !dataSource.IsValueCreated)
        {
            return false;
        }

        dataSource.Value.Dispose();
        return true;
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
