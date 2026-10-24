using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Creates connections only through the ADO.NET abstractions (<see cref="DbDataSource" /> or
///     <see cref="DbProviderFactory" />). EF's base class opens late and closes early around each operation,
///     so pooling stays with the underlying provider.
/// </summary>
public class AdoNetRelationalConnection : RelationalConnection
{
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

        if (_extension.ProviderFactory != null && string.IsNullOrWhiteSpace(_extension.ConnectionString))
        {
            throw new InvalidOperationException("A DbProviderFactory requires a connection string.");
        }
    }

    protected override DbConnection CreateDbConnection()
    {
        if (_extension.DataSource is { } dataSource)
        {
            return dataSource.CreateConnection();
        }

        var factory = _extension.ProviderFactory!;
        var connection = factory.CreateConnection()
            ?? throw new InvalidOperationException($"{factory.GetType().Name}.CreateConnection() returned null.");

        connection.ConnectionString = ConnectionString;
        return connection;
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
