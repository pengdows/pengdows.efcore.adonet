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
    private readonly string? _sessionSetup;

    // Connections whose sessions get the dialect's setup, with it; contexts can share one connection, which is watched once.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbConnection, string> Watched = new();

    public AdoNetRelationalConnection(
        RelationalConnectionDependencies dependencies,
        AdoNetDataSources dataSources,
        IDatabaseCapabilities capabilities,
        ISqlDialect sql)
        : base(dependencies)
    {
        _dataSources = dataSources;
        _capabilities = capabilities;
        _sessionSetup = sql.SessionSetup;
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

    // The dialect's session setup runs as each connection opens, whoever opens it (EF, or the application), before anything
    // else can run on it; a connection already open when EF first sees it is set up then, in EF's transaction if it has one.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override DbConnection DbConnection
    {
        get
        {
            var connection = base.DbConnection;
            if ((_sessionSetup != null) && !Watched.TryGetValue(connection, out _))
            {
                Watch(connection, _sessionSetup, CurrentTransaction?.GetDbTransaction());
            }

            return connection;
        }
        set => base.DbConnection = value;
    }

    private static void Watch(DbConnection connection, string setup, DbTransaction? transaction)
    {
        Watched.AddOrUpdate(connection, setup);
        connection.StateChange += SessionStarted;
        if (connection.State == ConnectionState.Open)
        {
            Run(connection, setup, transaction);
        }
    }

    private static void SessionStarted(object? sender, StateChangeEventArgs e)
    {
        if ((e.CurrentState == ConnectionState.Open) && (sender is DbConnection connection) && Watched.TryGetValue(connection, out var setup))
        {
            Run(connection, setup, transaction: null);
        }
    }

    private static void Run(DbConnection connection, string setup, DbTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.CommandText = setup;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
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

    // Some drivers' DisposeAsync skips what Dispose does (one neither raises Disposed nor disposes, another raises nothing):
    // close asynchronously, which does the I/O, then dispose synchronously, which every driver gets right and which costs
    // nothing on a closed connection.
    // Logged and intercepted as EF's base does. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Relational/Storage/RelationalConnection.cs
    protected override async ValueTask DisposeDbConnectionAsync()
    {
        await CloseDbConnectionAsync();

        var logger = Dependencies.ConnectionLogger;
        var startTime = DateTimeOffset.UtcNow;
        if (!logger.ShouldLogConnectionDispose(startTime))
        {
            DbConnection.Dispose();
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (!(await logger.ConnectionDisposingAsync(this, startTime)).IsSuppressed)
        {
            DbConnection.Dispose();
        }

        await logger.ConnectionDisposedAsync(this, startTime, stopwatch.Elapsed);
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
