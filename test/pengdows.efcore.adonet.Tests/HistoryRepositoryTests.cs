using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>The ISO history table and its lock, on fakeDb.</summary>
public class HistoryRepositoryTests
{
    private const string LockInsert = "INSERT INTO \"__EFMigrationsLock\"";
    private const string LockTableCreate = "CREATE TABLE \"__EFMigrationsLock\"";
    private const string AppLockInsert = "INSERT INTO \"app\".\"__EFMigrationsLock\"";
    private const string HolderQuery = "SELECT \"Timestamp\" FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1";

    private static string Stamp(DateTimeOffset at)
        => at.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private ItemContext Context(IAdoNetDialect? dialect = null, Action<DbContextOptionsBuilder>? configure = null)
    {
        var builder = new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(_db.Connection, contextOwnsConnection: false, o => o.UseDialect(dialect ?? AdoNetDialect.Ansi).MigrationsHistoryTable("History", "app"))
            .EnableSensitiveDataLogging()
            .AllowManyDialects();
        configure?.Invoke(builder);
        return new ItemContext(builder.Options);
    }

    [Fact]
    public async Task Existence_is_read_from_information_schema()
    {
        var log = new List<string>();
        await using var context = Context(configure: b => b.LogTo(log.Add, [RelationalEventId.CommandExecuted]));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1L);
        Assert.True(history.Exists());
        _db.Connection.EnqueueScalarResult(0);
        Assert.False(await history.ExistsAsync(Ct));
        _db.Connection.EnqueueScalarResult(DBNull.Value);
        Assert.False(history.Exists());

        Assert.Contains(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'History' AND TABLE_SCHEMA = 'app'",
            log[0]);
    }

    [Fact]
    public void Without_a_schema_the_check_is_in_the_dialects_current_schema()
    {
        var log = new List<string>();
        var dialect = new PartsDialect("Current schema", new PartsSql(currentSchema: "CURRENT_SCHEMA"));
        using var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(_db.Connection, contextOwnsConnection: false, o => o.UseDialect(dialect))
            .LogTo(log.Add, [RelationalEventId.CommandExecuted])
            .AllowManyDialects()
            .Options);

        _db.Connection.EnqueueScalarResult(0);
        context.GetService<IHistoryRepository>().Exists();

        Assert.Contains(
            "WHERE TABLE_NAME = '__EFMigrationsHistory' AND TABLE_SCHEMA = CURRENT_SCHEMA", log[0]);
    }

    [Fact]
    public void Dialects_without_schemas_check_the_name_only()
    {
        var log = new List<string>();
        using var context = Context(TestDialects.ColonParameters, b => b.LogTo(log.Add, [RelationalEventId.CommandExecuted]));

        context.GetService<IHistoryRepository>().Exists();

        Assert.EndsWith("WHERE TABLE_NAME = 'History'", log[0].TrimEnd());
    }

    [Fact]
    public async Task The_table_is_created_only_when_missing()
    {
        var commands = new CommandLog();
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        Assert.False(history.CreateIfNotExists());
        _db.Connection.EnqueueScalarResult(0);
        Assert.True(history.CreateIfNotExists());
        _db.Connection.EnqueueScalarResult(1);
        Assert.False(await history.CreateIfNotExistsAsync(Ct));
        _db.Connection.EnqueueScalarResult(0);
        Assert.True(await history.CreateIfNotExistsAsync(Ct));

        Assert.Equal(2, commands.NonQueries.Count(c => c.StartsWith("CREATE TABLE \"app\".\"History\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void Scripts_use_create_if_not_exists_and_refuse_conditional_blocks()
    {
        using var context = Context(TestDialects.Top);
        var history = context.GetService<IHistoryRepository>();

        Assert.Contains("CREATE TABLE IF NOT EXISTS \"app\".\"History\"", history.GetCreateIfNotExistsScript());
        Assert.Contains("'Top'", Assert.Throws<NotSupportedException>(() => history.GetBeginIfNotExistsScript("m")).Message);
        Assert.Throws<NotSupportedException>(() => history.GetBeginIfExistsScript("m"));
        Assert.Throws<NotSupportedException>(() => history.GetEndIfScript());
        Assert.Equal(LockReleaseBehavior.Explicit, history.LockReleaseBehavior);
    }

    [Fact]
    public async Task The_lock_is_a_row_in_a_lock_table_created_on_first_use()
    {
        var commands = new CommandLog();
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(0);
        using (history.AcquireDatabaseLock())
        {
        }

        _db.Connection.EnqueueScalarResult(1);
        await using (await history.AcquireDatabaseLockAsync(Ct))
        {
        }

        Assert.Equal(1, commands.NonQueries.Count(c => c.StartsWith("CREATE TABLE \"app\".\"__EFMigrationsLock\"", StringComparison.Ordinal)));
        Assert.Equal(2, commands.NonQueries.Count(c => c.StartsWith("INSERT INTO \"app\".\"__EFMigrationsLock\" (\"Id\", \"Timestamp\") VALUES (1, '", StringComparison.Ordinal)));
        Assert.Equal(2, commands.NonQueries.Count(c => c == "DELETE FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1"));
    }

    [Fact]
    public void An_explicit_lock_survives_reconnects_and_restarted_transactions()
    {
        using var context = Context();
        var history = context.GetService<IHistoryRepository>();
        _db.Connection.EnqueueScalarResult(1);

        using var databaseLock = history.AcquireDatabaseLock();

        Assert.Same(databaseLock, databaseLock.ReacquireIfNeeded(connectionReopened: true, transactionRestarted: true));
    }

    [Fact]
    public async Task A_held_lock_is_waited_for()
    {
        var commands = new CommandLog { FailFirst = AppLockInsert, Holder = Stamp(DateTimeOffset.UtcNow) };
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        using (history.AcquireDatabaseLock())
        {
        }

        commands.Reset();
        _db.Connection.EnqueueScalarResult(1);
        await using (await history.AcquireDatabaseLockAsync(Ct))
        {
        }

        // Each acquisition: one refused insert, the holder looked up, then one insert that succeeds.
        Assert.Equal(4, commands.NonQueries.Count(c => c.StartsWith("INSERT INTO", StringComparison.Ordinal)));
        Assert.Equal(2, commands.Scalars.Count(c => c == HolderQuery));
    }

    [Fact]
    public async Task A_lock_that_is_never_released_times_out_naming_its_holder()
    {
        var since = DateTimeOffset.UtcNow;
        var commands = new CommandLog { FailAlways = "INSERT INTO", Holder = Stamp(since) };
        await using var context = Context(ShortLockDialect.Instance, b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        var timedOut = Assert.Throws<TimeoutException>(() => history.AcquireDatabaseLock());
        _db.Connection.EnqueueScalarResult(1);
        await Assert.ThrowsAsync<TimeoutException>(() => history.AcquireDatabaseLockAsync(Ct));

        Assert.Contains(Stamp(since), timedOut.Message);
        Assert.IsType<LockConflict>(timedOut.InnerException);
    }

    [Fact]
    public async Task A_lock_older_than_the_timeout_is_taken_over()
    {
        var stale = Stamp(DateTimeOffset.UtcNow - TimeSpan.FromHours(1));
        var commands = new CommandLog { FailFirst = AppLockInsert, Holder = stale };
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        await using (await history.AcquireDatabaseLockAsync(Ct))
        {
        }

        commands.Reset();
        _db.Connection.EnqueueScalarResult(1);
        using (history.AcquireDatabaseLock())
        {
        }

        // Only that holder's row: a migrator that took the lock meanwhile keeps it.
        Assert.Equal(2, commands.NonQueries.Count(
            c => c == $"DELETE FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1 AND \"Timestamp\" = '{stale}'"));
        Assert.Equal(4, commands.NonQueries.Count(c => c.StartsWith(AppLockInsert, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task An_insert_refused_while_no_lock_is_held_is_a_real_error()
    {
        var commands = new CommandLog { FailAlways = AppLockInsert, Holder = null };
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        Assert.Throws<LockConflict>(() => history.AcquireDatabaseLock());
        _db.Connection.EnqueueScalarResult(1);
        await Assert.ThrowsAsync<LockConflict>(() => history.AcquireDatabaseLockAsync(Ct));

        // A second try covers a lock released between the insert and the look-up; then the error stands.
        Assert.Equal(4, commands.NonQueries.Count(c => c.StartsWith(AppLockInsert, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_failed_migrations_transaction_is_rolled_back_before_a_synchronous_release_too()
    {
        var events = new List<string>();
        var commands = new CommandLog { Events = events };
        using var context = Context(configure: b => b.AddInterceptors(commands, new TransactionLog(events)));
        var history = context.GetService<IHistoryRepository>();
        _db.Connection.EnqueueScalarResult(1);

        var databaseLock = history.AcquireDatabaseLock();
        using var transaction = context.Database.BeginTransaction();
        databaseLock.Dispose();

        Assert.Equal(["rollback", "DELETE FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1"], events.Skip(1));
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task A_lock_that_cannot_be_released_says_so()
    {
        await using var context = Context();
        var history = context.GetService<IHistoryRepository>();
        _db.Connection.EnqueueScalarResult(1);
        var databaseLock = history.AcquireDatabaseLock();
        _db.Connection.SetCommandFailure("DELETE FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1", new LockConflict());

        // Not swallowed: a lock left behind blocks every later migration until it goes stale.
        Assert.Throws<LockConflict>(() => databaseLock.Dispose());
        await Assert.ThrowsAsync<LockConflict>(() => databaseLock.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task A_rollback_that_fails_before_the_release_surfaces()
    {
        await using var context = Context();
        var history = context.GetService<IHistoryRepository>();
        _db.Connection.EnqueueScalarResult(1);
        var databaseLock = await history.AcquireDatabaseLockAsync(Ct);
        _db.Connection.SetTransactionRollbackException(new LockConflict());
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        await Assert.ThrowsAsync<LockConflict>(() => databaseLock.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task A_holder_that_cannot_be_looked_up_surfaces_without_retrying()
    {
        var commands = new CommandLog { FailAlways = AppLockInsert, HolderFails = true };
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        Assert.Throws<HolderUnavailable>(() => history.AcquireDatabaseLock());
        _db.Connection.EnqueueScalarResult(1);
        await Assert.ThrowsAsync<HolderUnavailable>(() => history.AcquireDatabaseLockAsync(Ct));

        Assert.Equal(2, commands.NonQueries.Count(c => c.StartsWith(AppLockInsert, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_failed_migrations_transaction_is_rolled_back_before_the_lock_is_released()
    {
        var events = new List<string>();
        var commands = new CommandLog { Events = events };
        await using var context = Context(configure: b => b.AddInterceptors(commands, new TransactionLog(events)));
        var history = context.GetService<IHistoryRepository>();
        _db.Connection.EnqueueScalarResult(1);

        // As EF's migrator does: the lock, then a transaction; on failure the lock is disposed before the transaction.
        var databaseLock = await history.AcquireDatabaseLockAsync(Ct);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        await databaseLock.DisposeAsync();

        Assert.Equal(["rollback", "DELETE FROM \"app\".\"__EFMigrationsLock\" WHERE \"Id\" = 1"], events.Skip(1));
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Another_migrator_creating_the_lock_table_first_is_not_an_error()
    {
        var commands = new CommandLog { FailFirst = "CREATE TABLE" };
        await using var context = Context(configure: b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        // Missing, the CREATE fails, and then it's there.
        _db.Connection.EnqueueScalarResult(0);
        _db.Connection.EnqueueScalarResult(1);
        using (history.AcquireDatabaseLock())
        {
        }

        commands.Reset();
        _db.Connection.EnqueueScalarResult(0);
        _db.Connection.EnqueueScalarResult(1);
        await using (await history.AcquireDatabaseLockAsync(Ct))
        {
        }

        // Still missing after the failure: the failure is real.
        commands.Reset();
        _db.Connection.EnqueueScalarResult(0);
        _db.Connection.EnqueueScalarResult(0);
        Assert.Throws<LockConflict>(() => history.AcquireDatabaseLock());
        commands.Reset();
        _db.Connection.EnqueueScalarResult(0);
        _db.Connection.EnqueueScalarResult(0);
        await Assert.ThrowsAsync<LockConflict>(() => history.AcquireDatabaseLockAsync(Ct));
    }

    private sealed class LockConflict() : DbException("conflict");

    private sealed class HolderUnavailable() : DbException("holder unavailable");

    private sealed class TransactionLog(List<string> events) : DbTransactionInterceptor
    {
        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            events.Add("rollback");
            return Task.CompletedTask;
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
            => events.Add("rollback");
    }

    /// <summary>
    ///     Records SQL; can fail the first, or every, non-query starting with some text, and answers the lock-holder query
    ///     with <see cref="Holder" />.
    /// </summary>
    private sealed class CommandLog : DbCommandInterceptor
    {
        private bool _failed;

        public List<string> NonQueries { get; } = [];
        public List<string> Scalars { get; } = [];
        public List<string>? Events { get; init; }
        public string? FailFirst { get; init; }
        public string? FailAlways { get; init; }
        public string? Holder { get; init; }
        public bool HolderFails { get; init; }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
            => AnswerHolder(command, result);

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(AnswerHolder(command, result));

        private InterceptionResult<object> AnswerHolder(DbCommand command, InterceptionResult<object> result)
        {
            var sql = command.CommandText.ReplaceLineEndings("\n");
            Scalars.Add(sql);
            if (HolderFails && (sql == HolderQuery))
            {
                throw new HolderUnavailable();
            }

            return sql == HolderQuery ? InterceptionResult<object>.SuppressWithResult(Holder ?? (object)DBNull.Value) : result;
        }

        public void Reset()
            => _failed = false;

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Check(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return ValueTask.FromResult(result);
        }

        private void Check(DbCommand command)
        {
            var sql = command.CommandText.ReplaceLineEndings("\n");
            NonQueries.Add(sql);
            Events?.Add(sql);
            if (FailAlways != null && sql.StartsWith(FailAlways, StringComparison.Ordinal))
            {
                throw new LockConflict();
            }

            if (!_failed && FailFirst != null && sql.StartsWith(FailFirst, StringComparison.Ordinal))
            {
                _failed = true;
                throw new LockConflict();
            }
        }
    }

    private sealed class ShortLockDialect : AdoNetDialect
    {
        public static readonly ShortLockDialect Instance = new();

        public override string Name => "Short lock";

        public override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            => new EntityFrameworkRelationalServicesBuilder(services).TryAdd<IHistoryRepository, ShortLockHistory>();

        private sealed class ShortLockHistory(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
            : AdoNetHistoryRepository(dependencies, dialect)
        {
            protected override TimeSpan LockTimeout => TimeSpan.FromMilliseconds(150);
        }
    }
}
