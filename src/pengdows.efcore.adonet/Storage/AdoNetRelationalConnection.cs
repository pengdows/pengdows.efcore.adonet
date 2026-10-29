using System.Data;
using System.Data.Common;
using Transaction = System.Transactions.Transaction;
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
    private readonly IsolationLevel _defaultIsolationLevel;
    private readonly string? _sessionSetup;
    private readonly IReadOnlyDictionary<string, string> _connectionOptions;

    // Connections whose sessions get the dialect's setup, with it; contexts can share one connection, which is watched once.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbConnection, string> Watched = new();

    // Connections already given the dialect's connection options, or found to have them.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbConnection, object> Optioned = new();

    public AdoNetRelationalConnection(
        RelationalConnectionDependencies dependencies,
        AdoNetDataSources dataSources,
        IDatabaseCapabilities capabilities,
        ISqlDialect sql)
        : base(dependencies)
    {
        _dataSources = dataSources;
        _capabilities = capabilities;
        _defaultIsolationLevel = sql.DefaultTransactionIsolationLevel;
        _sessionSetup = sql.SessionSetup;
        _connectionOptions = sql.ConnectionOptions;
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

    /// <summary>Whether this context's connections hold several open readers, which can depend on its connection string.</summary>
    internal bool SupportsMultipleActiveReaders
        => _capabilities.SupportsMultipleActiveReadersFor(ConnectionString ?? _extension.DataSource?.ConnectionString);

    // ConnectionString, not the options' value, so Database.SetConnectionString is honored; a connection string may
    // also arrive that way, or a whole connection through SetDbConnection, after the context is built.
    protected override DbConnection CreateDbConnection()
    {
        if (_extension.DataSource is { } dataSource)
        {
            if (WithOptions(null, dataSource.ConnectionString) != null)
            {
                throw MissingOptions("data source");
            }

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

        return _dataSources.GetOrCreate(factory, ConnectionString, cs => DataSourceConnectionString(factory, cs)).CreateConnection();
    }

    /// <summary>The connection string the data source for <paramref name="connectionString" /> is made from: with the dialect's options.</summary>
    internal string DataSourceConnectionString(DbProviderFactory factory, string connectionString)
        => WithOptions(factory, connectionString) ?? connectionString;

    // The connection string with the dialect's options added, or null when it has them all already.
    private string? WithOptions(DbProviderFactory? factory, string connectionString)
    {
        if (_connectionOptions.Count == 0)
        {
            return null;
        }

        var builder = factory?.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        builder.ConnectionString = connectionString;
        var missing = false;
        foreach (var (key, value) in _connectionOptions)
        {
            if (!builder.TryGetValue(key, out var current)
                || !string.Equals(Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture), value, StringComparison.OrdinalIgnoreCase))
            {
                builder[key] = value;
                missing = true;
            }
        }

        return missing ? builder.ConnectionString : null;
    }

    // A connection the application gives the provider gets the options while it is closed; once open, it can't.
    private void AddOptions(DbConnection connection)
    {
        if (WithOptions(DbProviderFactories.GetFactory(connection), connection.ConnectionString) is { } connectionString)
        {
            if (connection.State != ConnectionState.Closed)
            {
                throw MissingOptions("open connection");
            }

            connection.ConnectionString = connectionString;
        }

        Optioned.AddOrUpdate(connection, Optioned);
    }

    private InvalidOperationException MissingOptions(string source)
        => new(
            $"The {source} lacks connection string options this dialect's SQL relies on, and its connection string can't be changed: "
            + "include " + string.Join(";", _connectionOptions.Select(o => o.Key + "=" + o.Value)) + " in it.");

    // The dialect's session setup runs as each connection opens, whoever opens it (EF, or the application), before anything
    // else can run on it; a connection already open when EF first sees it is set up then, in EF's transaction if it has one.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override DbConnection DbConnection
    {
        get
        {
            var connection = base.DbConnection;
            if ((_connectionOptions.Count > 0) && !Optioned.TryGetValue(connection, out _))
            {
                AddOptions(connection);
            }

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
            return base.BeginTransaction(DefaultIsolationLevel(isolationLevel));
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
            return await base.BeginTransactionAsync(DefaultIsolationLevel(isolationLevel), cancellationToken);
        }
        catch
        {
            await CloseAsync();
            throw;
        }
    }

    private IsolationLevel DefaultIsolationLevel(IsolationLevel isolationLevel)
        => isolationLevel == IsolationLevel.Unspecified
            ? _defaultIsolationLevel
            : isolationLevel;

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

    // EF reports enlisting in a transaction a connection can't take (one already enlisted, in an ambient transaction, or with
    // a local transaction begun) as InvalidOperationException, whatever the driver throws for it. Every other driver failure
    // keeps its type and error code.
    protected override void ConnectionEnlistTransaction(Transaction? transaction)
    {
        try
        {
            base.ConnectionEnlistTransaction(transaction);
        }
        catch (DbException exception) when ((transaction != null)
            && ((EnlistedTransaction != null) || (CurrentTransaction != null) || (Transaction.Current != null)))
        {
            throw new InvalidOperationException("The connection is already in a transaction.", exception);
        }
    }
}
