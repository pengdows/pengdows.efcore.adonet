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
        var commands = new CommandLog { FailFirst = LockInsert.Replace("\"__", "\"app\".\"__", StringComparison.Ordinal) };
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

        // Each acquisition: one refused insert, then one that succeeds.
        Assert.Equal(4, commands.NonQueries.Count(c => c.StartsWith("INSERT INTO", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_lock_that_is_never_released_times_out()
    {
        var commands = new CommandLog { FailAlways = "INSERT INTO" };
        await using var context = Context(ShortLockDialect.Instance, b => b.AddInterceptors(commands));
        var history = context.GetService<IHistoryRepository>();

        _db.Connection.EnqueueScalarResult(1);
        Assert.Throws<LockConflict>(() => history.AcquireDatabaseLock());
        _db.Connection.EnqueueScalarResult(1);
        await Assert.ThrowsAsync<LockConflict>(() => history.AcquireDatabaseLockAsync(Ct));
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

    /// <summary>Records non-query SQL; can fail the first, or every, command starting with some text.</summary>
    private sealed class CommandLog : DbCommandInterceptor
    {
        private bool _failed;

        public List<string> NonQueries { get; } = [];
        public string? FailFirst { get; init; }
        public string? FailAlways { get; init; }

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
