using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     A retrying execution strategy (<c>EnableRetryOnFailure</c>) retries what fails transiently, opening, querying, saving,
///     beginning and committing, and only that: a commit that failed after it succeeded is verified, not repeated.
/// </summary>
/// <remarks>
///     Covers SQL Server's and Npgsql's <c>ExecutionStrategyTest</c>. They inject failures through a provider-specific
///     connection; here EF's own interceptors inject them, so the same test runs on every database. The failure is a
///     <see cref="DbException" /> that reports itself transient, which every dialect's capabilities retry. Runs on: MySQL,
///     PostgreSQL, SQL Server, flatfile, SQLite and DuckDB. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/test/EFCore.PG.FunctionalTests/ExecutionStrategyTest.cs
/// </remarks>
public abstract class ExecutionStrategyConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();
        await Fixture.ResetAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private (BlogContext Context, FailureInjector Failures, List<string> Retries) Context(int maxRetryCount = 6)
    {
        var failures = new FailureInjector();
        var retries = new List<string>();
        var builder = new DbContextOptionsBuilder<BlogContext>(Fixture.Options)
            .AddInterceptors(failures)
            .LogTo(retries.Add, [CoreEventId.ExecutionStrategyRetrying]);
        new AdoNetDbContextOptionsBuilder(builder)
            .ExecutionStrategy(d => new ImmediateRetry(d, Fixture.ExpectedDialect.Capabilities, maxRetryCount));
        return (new BlogContext(builder.Options), failures, retries);
    }

    private static Blog NewBlog(string name)
        => new() { Name = name, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() };

    private async Task SeedAsync(int count)
    {
        await using var context = Fixture.CreateContext();
        for (var i = 0; i < count; i++)
        {
            context.Add(NewBlog($"seeded{i}"));
        }

        await context.SaveChangesAsync(Ct);
    }

    private async Task<int> CountAsync()
    {
        await using var context = Fixture.CreateContext();
        return await context.Blogs.CountAsync(Ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_query_is_retried(bool async)
    {
        await SeedAsync(2);
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Commands.Enqueue(FailureInjector.Before);

        var blogs = async ? await context.Blogs.ToListAsync(Ct) : context.Blogs.ToList();

        Assert.Equal(2, blogs.Count);
        Assert.Equal(2, failures.Executions);
        Assert.Single(retries);
    }

    [Fact]
    public async Task A_raw_SQL_query_is_retried()
    {
        await SeedAsync(2);
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Commands.Enqueue(FailureInjector.Before);

        // A table name can't be a parameter; the quoted name is the fixture's own.
        var sql = "SELECT * FROM " + Fixture.Quote("Blogs");
        var blogs = await context.Blogs.FromSqlRaw(sql).ToListAsync(Ct);

        Assert.Equal(2, blogs.Count);
        Assert.Single(retries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Opening_the_connection_is_retried(bool async)
    {
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Opens.Enqueue(FailureInjector.Before);

        if (async)
        {
            await context.Database.CreateExecutionStrategy().ExecuteAsync(() => context.Database.OpenConnectionAsync(Ct));
        }
        else
        {
            context.Database.CreateExecutionStrategy().Execute(() => context.Database.OpenConnection());
        }

        Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
        Assert.Single(retries);
        await context.Database.CloseConnectionAsync();
    }

    [Fact]
    public async Task Beginning_a_transaction_is_retried()
    {
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Begins.Enqueue(FailureInjector.Before);

        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Add(NewBlog("in a transaction"));
            await context.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
        });

        Assert.Single(retries);
        Assert.Equal(1, await CountAsync());
    }

    // Before: the command never ran. After: it ran, in SaveChanges' transaction, which the failure rolls back. Either way
    // the retry saves both rows once.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Save_changes_is_retried_and_saves_once(bool failsBeforeRunning, bool async)
    {
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Commands.Enqueue(failsBeforeRunning ? FailureInjector.Before : FailureInjector.After);
        context.AddRange(NewBlog("first"), NewBlog("second"));

        if (async)
        {
            await context.SaveChangesAsync(Ct);
        }
        else
        {
            context.SaveChanges();
        }

        Assert.Single(retries);
        Assert.Equal(2, await CountAsync());
    }

    // A commit that fails before committing is retried; one that fails after committing is checked by the verification,
    // which finds the row, so nothing is retried and nothing saved twice.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task A_failed_commit_is_retried_only_if_it_did_not_commit(bool failsBeforeCommitting, bool async)
    {
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Commits.Enqueue(failsBeforeCommitting ? FailureInjector.Before : FailureInjector.After);
        context.Add(NewBlog("committed"));
        var strategy = context.Database.CreateExecutionStrategy();

        if (async)
        {
            await strategy.ExecuteInTransactionAsync(
                context, (c, ct) => c.SaveChangesAsync(false, ct), (c, ct) => c.Blogs.AsNoTracking().AnyAsync(ct), Ct);
        }
        else
        {
            strategy.ExecuteInTransaction(context, c => c.SaveChanges(false), c => c.Blogs.AsNoTracking().Any());
        }

        context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(failsBeforeCommitting ? 1 : 0, retries.Count);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Verifying_a_commit_counts_against_the_same_retry_limit()
    {
        var (context, failures, _) = Context(maxRetryCount: 2);
        await using var __ = context;
        for (var i = 0; i < 10; i++)
        {
            failures.Commits.Enqueue(FailureInjector.After);
        }

        context.Add(NewBlog("verified"));
        var verifications = 0;

        await Assert.ThrowsAsync<RetryLimitExceededException>(() => context.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(
            context,
            (c, ct) => c.SaveChangesAsync(false, ct),
            (_, _) =>
            {
                verifications++;
                throw new TransientFailure();
            },
            Ct));

        Assert.Equal(3, verifications);
    }

    [Fact]
    public async Task A_failure_that_is_not_transient_is_not_retried()
    {
        var (context, failures, retries) = Context();
        await using var _ = context;
        failures.Commands.Enqueue(FailureInjector.Permanent);

        await Assert.ThrowsAnyAsync<Exception>(() => context.Blogs.ToListAsync(Ct));

        Assert.Equal(1, failures.Executions);
        Assert.Empty(retries);
    }

    /// <summary>Retries at once, so the tests don't wait out EF's back-off.</summary>
    private sealed class ImmediateRetry(ExecutionStrategyDependencies dependencies, IDatabaseCapabilities capabilities, int maxRetryCount)
        : AdoNetRetryingExecutionStrategy(dependencies, capabilities, maxRetryCount)
    {
        protected override TimeSpan? GetNextDelay(Exception lastException)
            => base.GetNextDelay(lastException) == null ? null : TimeSpan.Zero;
    }

    /// <summary>A failure the database might not repeat: what a deadlock or a dropped connection reports.</summary>
    private sealed class TransientFailure() : DbException("A transient failure, injected by the test.")
    {
        public override bool IsTransient
            => true;
    }

    /// <summary>A failure that retrying won't fix.</summary>
    private sealed class PermanentFailure() : DbException("A permanent failure, injected by the test.");

    /// <summary>
    ///     Fails the next operations of each kind as queued: <see cref="Before" /> it runs, <see cref="After" /> it has run, or
    ///     <see cref="Permanent" />ly; an empty queue lets it through.
    /// </summary>
    private sealed class FailureInjector : IDbConnectionInterceptor, IDbCommandInterceptor, IDbTransactionInterceptor
    {
        public const string Before = "before";
        public const string After = "after";
        public const string Permanent = "permanent";

        private bool _failAfterCommand;
        private bool _failAfterCommit;

        public Queue<string> Opens { get; } = new();
        public Queue<string> Commands { get; } = new();
        public Queue<string> Begins { get; } = new();
        public Queue<string> Commits { get; } = new();

        /// <summary>Commands that reached the database or were failed on the way.</summary>
        public int Executions { get; private set; }

        private static void FailBefore(Queue<string> queue, ref bool failAfter)
        {
            if (!queue.TryDequeue(out var failure))
            {
                return;
            }

            switch (failure)
            {
                case Before:
                    throw new TransientFailure();
                case Permanent:
                    throw new PermanentFailure();
                default:
                    failAfter = true;
                    break;
            }
        }

        private static void FailAfter(ref bool failAfter)
        {
            if (failAfter)
            {
                failAfter = false;
                throw new TransientFailure();
            }
        }

        private void Executing()
        {
            Executions++;
            FailBefore(Commands, ref _failAfterCommand);
        }

        public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            var ignored = false;
            FailBefore(Opens, ref ignored);
            return result;
        }

        public ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ConnectionOpening(connection, eventData, result));

        public InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Executing();
            return result;
        }

        public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ReaderExecuting(command, eventData, result));

        public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Executing();
            return result;
        }

        public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(NonQueryExecuting(command, eventData, result));

        public InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Executing();
            return result;
        }

        public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ScalarExecuting(command, eventData, result));

        public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            if (_failAfterCommand)
            {
                result.Dispose();
            }

            FailAfter(ref _failAfterCommand);
            return result;
        }

        public ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ReaderExecuted(command, eventData, result));

        public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            FailAfter(ref _failAfterCommand);
            return result;
        }

        public ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(NonQueryExecuted(command, eventData, result));

        public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            FailAfter(ref _failAfterCommand);
            return result;
        }

        public ValueTask<object?> ScalarExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ScalarExecuted(command, eventData, result));

        public InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
        {
            var ignored = false;
            FailBefore(Begins, ref ignored);
            return result;
        }

        public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TransactionStarting(connection, eventData, result));

        public InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            FailBefore(Commits, ref _failAfterCommit);
            return result;
        }

        public ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TransactionCommitting(transaction, eventData, result));

        public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
            => FailAfter(ref _failAfterCommit);

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            FailAfter(ref _failAfterCommit);
            return Task.CompletedTask;
        }
    }
}
