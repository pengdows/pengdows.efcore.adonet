using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Creates connections only through <see cref="DbDataSource" />: the caller's, or one made by
///     <see cref="DbProviderFactory.CreateDataSource" />. EF's base class opens late and closes early around each operation,
///     so pooling stays with the underlying provider.
/// </summary>
public class AdoNetRelationalConnection : RelationalConnection
{
    // Drivers with their own data source (Npgsql, MySqlConnector) pool per data source, so one per context would mean a pool
    // per context. Like the drivers' global pools, these live for the process. Lazy keeps a lost race from leaking one.
    private static readonly ConcurrentDictionary<(DbProviderFactory Factory, string ConnectionString), Lazy<DbDataSource>> DataSources
        = new();

    private readonly AdoNetOptionsExtension _extension;

    public AdoNetRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
        _extension = dependencies.ContextOptions.FindExtension<AdoNetOptionsExtension>()
            ?? throw new InvalidOperationException($"{nameof(AdoNetOptionsExtension)} is not configured.");

        // Checked here rather than in IDbContextOptionsExtension.Validate, which EF only runs when it builds
        // a new internal service provider, not for every options instance.
        var sources = (_extension.Connection != null ? 1 : 0)
            + (_extension.DataSource != null ? 1 : 0)
            + (_extension.ProviderFactory != null ? 1 : 0);

        if (sources != 1)
        {
            throw new InvalidOperationException(
                "Configure exactly one of: a DbConnection, a DbDataSource, or a DbProviderFactory with a connection string.");
        }
    }

    // ConnectionString, not the options' value, so Database.SetConnectionString is honored; a connection string may
    // also arrive that way, or a whole connection through SetDbConnection, after the context is built.
    protected override DbConnection CreateDbConnection()
    {
        if (_extension.DataSource == null && string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "A DbProviderFactory requires a connection string: configure one, or call Database.SetConnectionString or Database.SetDbConnection first.");
        }

        return (_extension.DataSource
                ?? DataSources.GetOrAdd(
                        (_extension.ProviderFactory!, ConnectionString!),
                        static key => new Lazy<DbDataSource>(() => key.Factory.CreateDataSource(key.ConnectionString)))
                    .Value)
            .CreateConnection();
    }

    // DbConnection.Close is documented as safe to call on a closed connection and EF relies on that,
    // but not every provider honors it (DuckDB.NET throws), so only close what is open.
    protected override void CloseDbConnection()
    {
        if (DbConnection.State != ConnectionState.Closed)
        {
            base.CloseDbConnection();
        }
    }

    protected override Task CloseDbConnectionAsync()
        => DbConnection.State != ConnectionState.Closed
            ? base.CloseDbConnectionAsync()
            : Task.CompletedTask;
}
