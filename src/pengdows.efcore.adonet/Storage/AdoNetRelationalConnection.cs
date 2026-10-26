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
internal class AdoNetRelationalConnection : RelationalConnection
{
    private readonly AdoNetOptionsExtension _extension;
    private readonly AdoNetDataSources _dataSources;
    private readonly IDatabaseCapabilities _capabilities;

    public AdoNetRelationalConnection(
        RelationalConnectionDependencies dependencies,
        AdoNetDataSources dataSources,
        IDatabaseCapabilities capabilities)
        : base(dependencies)
    {
        _dataSources = dataSources;
        _capabilities = capabilities;
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

    protected override bool SupportsAmbientTransactions
        => _capabilities.SupportsAmbientTransactions;

    // ConnectionString, not the options' value, so Database.SetConnectionString is honored; a connection string may
    // also arrive that way, or a whole connection through SetDbConnection, after the context is built.
    protected override DbConnection CreateDbConnection()
    {
        if (_extension.DataSource is { } dataSource)
        {
            return dataSource.CreateConnection();
        }

        // Configured with a connection that was then removed (Database.SetDbConnection(null)): a connection string alone
        // can't say which driver to open it with.
        if (_extension.ProviderFactory is not { } factory)
        {
            throw new InvalidOperationException(
                "This context was configured with a DbConnection, so it has no DbProviderFactory to open a connection string with. "
                + "Call Database.SetDbConnection, or configure the context with a factory.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "A DbProviderFactory requires a connection string: configure one, or call Database.SetConnectionString or Database.SetDbConnection first.");
        }

        return _dataSources.GetOrCreate(factory, ConnectionString).CreateConnection();
    }

    // EF opens the connection to begin a transaction and, when beginning fails (a transient failure, an interceptor), leaves
    // it open with its open count raised, so the application's own Close no longer closes it. Close what the failed begin
    // opened; a connection the application opened stays open. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Relational/Storage/RelationalConnection.cs
    public override IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        try
        {
            return base.BeginTransaction(isolationLevel);
        }
        catch
        {
            Close();
            throw;
        }
    }

    public override async Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.BeginTransactionAsync(isolationLevel, cancellationToken);
        }
        catch
        {
            await CloseAsync();
            throw;
        }
    }

    // DbConnection.Close is documented as safe to call on a closed connection and EF relies on that,
    // but not every driver honors it (some throw), so only close what is open.
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
