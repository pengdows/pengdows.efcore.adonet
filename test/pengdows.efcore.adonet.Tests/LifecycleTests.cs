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

    [Fact]
    public void Savepoints_go_to_the_driver_and_failures_are_logged()
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

        // fakeDb reports no savepoint support and the dialect has no SQL for them: Save and Rollback(name) throw, Release is
        // DbTransaction's no-op.
        Assert.False(transaction.SupportsSavepoints);
        Assert.Throws<NotSupportedException>(() => transaction.CreateSavepoint("s1"));
        Assert.Throws<NotSupportedException>(() => transaction.RollbackToSavepoint("s1"));
        transaction.ReleaseSavepoint("s1");

        Assert.Equal(["CreateSavepoint", "RollbackToSavepoint"], failures.Actions);
    }

    [Fact]
    public async Task Savepoints_work_asynchronously_too()
    {
        var db = new FakeDatabase();
        await using var context = db.Context(TestDialects.NoSavepoints);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.CreateSavepointAsync("s1", Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() => transaction.RollbackToSavepointAsync("s1", Ct));
        await transaction.ReleaseSavepointAsync("s1", Ct);
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

    private sealed class TransactionFailures : DbTransactionInterceptor
    {
        public List<string> Actions { get; } = [];

        public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
            => Actions.Add(eventData.Action);
    }
}
