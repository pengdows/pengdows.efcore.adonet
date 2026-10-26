using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Open late, close early; who owns and disposes what; transactions, savepoints and failures.</summary>
public class LifecycleTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql";

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static ItemContext FromFactory(DbProviderFactory factory, Action<DbContextOptionsBuilder>? configure = null)
    {
        var builder = new DbContextOptionsBuilder<ItemContext>().UseAdoNet(factory, ConnectionString, AdoNetDialect.Ansi).AllowManyDialects();
        configure?.Invoke(builder);
        return new ItemContext(builder.Options);
    }

    // EF warns when a TransactionScope is active and the provider's connections don't enlist in it; where the database's
    // driver enlists (System.Transactions), EF asks it to. fakeDb has no enlistment (DbConnection.EnlistTransaction throws
    // NotSupportedException: UPSTREAM_ISSUES), so its refusal is the evidence EF asked; the enlistment itself is covered by
    // EF's Transaction specification suite on the server databases.
    [Theory]
    [InlineData(false, typeof(InvalidOperationException))]
    [InlineData(true, typeof(NotSupportedException))]
    public async Task An_ambient_transaction_is_enlisted_in_only_where_the_driver_can(bool enlists, Type outcome)
    {
        var dialect = new PartsDialect($"Ambient {enlists}", capabilities: new DatabaseCapabilities { SupportsAmbientTransactions = enlists });
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        await using var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(factory, ConnectionString, dialect)
            .ConfigureWarnings(w => w.Throw(RelationalEventId.AmbientTransactionWarning))
            .AllowManyDialects()
            .Options);
        using var scope = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

        var error = await Record.ExceptionAsync(() => context.Database.OpenConnectionAsync(Ct));

        Assert.IsType(outcome, error);
        Assert.Equal(enlists, error.StackTrace!.Contains("EnlistTransaction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_factory_connection_is_opened_per_operation_and_disposed_with_the_context()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        fakeDbConnection connection;

        using (var context = FromFactory(factory))
        {
            await context.Items.ToListAsync(Ct);
            await context.Items.ToListAsync(Ct);

            connection = Assert.Single(factory.CreatedConnections);
            Assert.Equal(2, connection.OpenCount);
            Assert.Equal(2, connection.CloseCount);
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task A_borrowed_connection_is_closed_but_never_disposed()
    {
        var db = new FakeDatabase();

        await using (var context = db.Context(AdoNetDialect.Ansi))
        {
            await context.Items.ToListAsync(Ct);
        }

        Assert.Equal(ConnectionState.Closed, db.Connection.State);
        Assert.Equal(0, db.Connection.DisposeCount);
    }

    [Fact]
    public async Task An_owned_connection_is_disposed_with_the_context()
    {
        var connection = new fakeDbConnection { ConnectionString = ConnectionString };

        // Sync dispose: fakeDb 2.0.5 counts a DisposeAsync twice.
        using (var context = new ItemContext(
                   new DbContextOptionsBuilder<ItemContext>().UseAdoNet(connection, contextOwnsConnection: true, o => o.UseDialect(AdoNetDialect.Ansi)).AllowManyDialects().Options))
        {
            await context.Items.ToListAsync(Ct);
        }

        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task A_connection_the_caller_opened_stays_open()
    {
        var db = new FakeDatabase();
        await db.Connection.OpenAsync(Ct);

        await using (var context = db.Context(AdoNetDialect.Ansi))
        {
            await context.Items.ToListAsync(Ct);
        }

        Assert.Equal(ConnectionState.Open, db.Connection.State);
        Assert.Equal(0, db.Connection.CloseCount);
    }

    [Fact]
    public async Task A_data_source_supplies_the_connections()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        using var dataSource = factory.CreateDataSource(ConnectionString);

        using (var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>().UseAdoNet(dataSource, o => o.UseDialect(AdoNetDialect.Ansi)).AllowManyDialects().Options))
        {
            await context.Items.ToListAsync(Ct);
        }

        // One probe connection to identify the provider, one for the context; both disposed, the data source is not.
        Assert.All(factory.CreatedConnections, c => Assert.Equal(1, c.DisposeCount));
        Assert.NotNull(dataSource.CreateConnection());
    }

    [Fact]
    public async Task A_transaction_keeps_the_connection_open_until_it_ends()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);

        await using (var transaction = await context.Database.BeginTransactionAsync(Ct))
        {
            await context.Items.ToListAsync(Ct);
            await context.Items.ToListAsync(Ct);
            Assert.Equal(ConnectionState.Open, db.Connection.State);

            await transaction.CommitAsync(Ct);
        }

        Assert.Equal(1, db.Connection.OpenCount);
        Assert.Equal(ConnectionState.Closed, db.Connection.State);
    }

    [Fact]
    public async Task A_failed_commit_is_reported_and_the_connection_still_closes()
    {
        var db = new FakeDatabase();
        db.Connection.SetTransactionCommitException(new InvalidOperationException("commit failed"));
        await using var context = db.Context(AdoNetDialect.Ansi);

        await using (var transaction = await context.Database.BeginTransactionAsync(Ct))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(Ct));
            Assert.Equal("commit failed", error.Message);
        }

        Assert.Equal(ConnectionState.Closed, db.Connection.State);
    }

    // Whether the database has savepoints is the dialect's to say, not the driver's: DbTransaction.SupportsSavepoints says
    // whether the driver implements the methods, not whether the server keeps them (MySqlConnector reports them on
    // SingleStore, which accepts SAVEPOINT and ROLLBACK TO and ignores them). Without savepoints all three refuse, as
    // DbTransaction specifies for SupportsSavepoints false.
    [Fact]
    public void A_dialect_without_savepoints_refuses_all_three_and_logs_it()
    {
        var failures = new TransactionFailures();
        var db = new FakeDatabase();
        using var context = new ItemContext(
            new DbContextOptionsBuilder<ItemContext>()
                .UseAdoNet(db.Connection, contextOwnsConnection: false, o => o.UseDialect(TestDialects.NoSavepoints))
                .AddInterceptors(failures)
                .AllowManyDialects()
                .Options);
        using var transaction = context.Database.BeginTransaction();

        Assert.False(transaction.SupportsSavepoints);
        Assert.Contains("No savepoints", Assert.Throws<NotSupportedException>(() => transaction.CreateSavepoint("s1")).Message);
        Assert.Throws<NotSupportedException>(() => transaction.RollbackToSavepoint("s1"));
        Assert.Throws<NotSupportedException>(() => transaction.ReleaseSavepoint("s1"));

        Assert.Equal(["CreateSavepoint", "RollbackToSavepoint", "ReleaseSavepoint"], failures.Actions);
        Assert.Empty(db.Commands);
    }

    [Fact]
    public async Task A_dialect_without_savepoints_refuses_them_asynchronously_too()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(TestDialects.NoSavepoints);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.CreateSavepointAsync("s1", Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.RollbackToSavepointAsync("s1", Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.ReleaseSavepointAsync("s1", Ct));
    }

    // Microsoft.Data.Sqlite implements savepoints (SupportsSavepoints true); a dialect for a server that ignores them must
    // still refuse, not hand them to the driver.
    [Fact]
    public async Task A_dialect_without_savepoints_has_none_even_where_the_driver_has_them()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await using var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(TestDialects.NoSavepoints))
            .AllowManyDialects()
            .Options);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        Assert.True(transaction.GetDbTransaction().SupportsSavepoints);
        Assert.False(transaction.SupportsSavepoints);
        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.CreateSavepointAsync("s1", Ct));
        Assert.Throws<NotSupportedException>(() => transaction.RollbackToSavepoint("s1"));
        Assert.Throws<NotSupportedException>(() => transaction.ReleaseSavepoint("s1"));
    }

    [Fact]
    public async Task Savepoints_the_driver_lacks_are_written_by_the_dialect()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        Assert.True(transaction.SupportsSavepoints);
        transaction.CreateSavepoint("s1");
        await transaction.RollbackToSavepointAsync("s1", Ct);
        await transaction.ReleaseSavepointAsync("s1", Ct);
        transaction.ReleaseSavepoint("s1");

        Assert.Equal(
            ["SAVEPOINT \"s1\"", "ROLLBACK TO SAVEPOINT \"s1\"", "RELEASE SAVEPOINT \"s1\"", "RELEASE SAVEPOINT \"s1\""],
            db.Commands);
    }

    [Fact]
    public async Task A_savepoint_statement_that_fails_surfaces_and_is_logged()
    {
        var failures = new TransactionFailures();
        var db = new FakeDatabase();
        await using var context = new ItemContext(
            new DbContextOptionsBuilder<ItemContext>()
                .UseAdoNet(db.Connection, contextOwnsConnection: false, o => o.UseDialect(AdoNetDialect.Ansi))
                .AddInterceptors(failures)
                .AllowManyDialects()
                .Options);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        db.Connection.SetCommandFailure("SAVEPOINT \"s1\"", new SavepointRefused());
        db.Connection.SetCommandFailure("ROLLBACK TO SAVEPOINT \"s1\"", new SavepointRefused());
        db.Connection.SetCommandFailure("RELEASE SAVEPOINT \"s1\"", new SavepointRefused());

        Assert.Throws<SavepointRefused>(() => transaction.CreateSavepoint("s1"));
        await Assert.ThrowsAsync<SavepointRefused>(() => transaction.RollbackToSavepointAsync("s1", Ct));
        await Assert.ThrowsAsync<SavepointRefused>(() => transaction.ReleaseSavepointAsync("s1", Ct));

        Assert.Equal(["CreateSavepoint", "RollbackToSavepoint", "ReleaseSavepoint"], failures.Actions);
        Assert.Same(transaction, context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task A_dialect_whose_savepoints_end_with_the_transaction_releases_nothing()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(TestDialects.TransactionSavepoints);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        await transaction.CreateSavepointAsync("s1", Ct);
        transaction.RollbackToSavepoint("s1");
        transaction.ReleaseSavepoint("s1");

        Assert.Equal(["SAVE TRANSACTION \"s1\"", "ROLLBACK TRANSACTION \"s1\""], db.Commands);
    }

    // EF opens the connection to begin a transaction and, if beginning fails, leaves it open (UPSTREAM_ISSUES): a retry, or
    // any later operation, then runs on a connection the context thinks it holds open.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_transaction_that_fails_to_begin_closes_the_connection_it_opened(bool async)
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        db.Connection.SetFailOnBeginTransaction();

        if (async)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => context.Database.BeginTransactionAsync(Ct));
        }
        else
        {
            Assert.ThrowsAny<Exception>(() => context.Database.BeginTransaction());
        }

        Assert.Equal(ConnectionState.Closed, db.Connection.State);
    }

    [Fact]
    public async Task A_transaction_that_fails_to_begin_leaves_a_connection_the_application_opened_open()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        await context.Database.OpenConnectionAsync(Ct);
        db.Connection.SetFailOnBeginTransaction();

        await Assert.ThrowsAnyAsync<Exception>(() => context.Database.BeginTransactionAsync(Ct));

        Assert.Equal(ConnectionState.Open, db.Connection.State);
        await context.Database.CloseConnectionAsync();
        Assert.Equal(ConnectionState.Closed, db.Connection.State);
    }

    [Fact]
    public async Task Closing_a_closed_connection_never_reaches_the_driver()
    {
        // Some drivers (DuckDB.NET) throw on a second Close(), despite DbConnection's contract.
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        await context.Items.ToListAsync(Ct);
        db.Connection.SetFailOnClose(new InvalidOperationException("closed twice"));

        context.Database.CloseConnection();
        await context.Database.CloseConnectionAsync();

        await context.Database.OpenConnectionAsync(Ct);
        Assert.Throws<InvalidOperationException>(() => context.Database.CloseConnection());
    }

    [Fact]
    public async Task Exists_is_false_only_when_the_database_cannot_be_reached()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        var creator = context.GetService<IRelationalDatabaseCreator>();

        Assert.True(creator.Exists());

        db.Connection.SetCustomFailureException(new UnreachableException());
        db.Connection.SetFailOnOpen();
        Assert.False(creator.Exists());
        Assert.False(await creator.ExistsAsync(Ct));

        db.Connection.SetCustomFailureException(new InvalidOperationException("not a database error"));
        Assert.Throws<InvalidOperationException>(() => creator.Exists());
    }

    [Fact]
    public async Task Has_tables_runs_the_dialects_query()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(AdoNetDialect.Ansi);
        var creator = context.GetService<IRelationalDatabaseCreator>();

        db.Connection.EnqueueScalarResult(3L);
        Assert.True(creator.HasTables());
        db.Connection.EnqueueScalarResult(0);
        Assert.False(await creator.HasTablesAsync(Ct));

        Assert.All(db.Commands, c => Assert.Equal(AnsiSqlDialect.Instance.HasTablesSql, c));
    }

    [Fact]
    public async Task Creating_or_deleting_a_database_names_the_dialect_in_its_refusal()
    {
        await using var context = new FakeDatabase().Context(TestDialects.Top);
        var creator = context.GetService<IRelationalDatabaseCreator>();

        Assert.Contains("'Top'", Assert.Throws<NotSupportedException>(() => creator.Create()).Message);
        Assert.Contains("'Top'", Assert.Throws<NotSupportedException>(() => creator.Delete()).Message);
    }

    private sealed class UnreachableException() : DbException("unreachable");

    private sealed class SavepointRefused() : DbException("savepoint refused");

    private sealed class TransactionFailures : DbTransactionInterceptor
    {
        public List<string> Actions { get; } = [];

        public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
            => Actions.Add(eventData.Action);

        public override Task TransactionFailedAsync(
            DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Actions.Add(eventData.Action);
            return Task.CompletedTask;
        }
    }
}
