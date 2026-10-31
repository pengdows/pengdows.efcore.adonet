using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Two migrators on one SQLite file exclude each other for as long as the first holds the lock, and a lock held longer than
///     the waiter's timeout is not taken over: the holder may still be migrating.
/// </summary>
public class SqliteMigrationLockTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pengdows-lock-{Guid.NewGuid():N}.db");

    private DbContextOptions<ProbeContext> Options
        => new DbContextOptionsBuilder<ProbeContext>().UseAdoNetSqlite($"Data Source={_path}").Options;

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + ".migrations.lock" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A lock still held at the end of a test is released with its process.
            }
        }
    }

    [Fact]
    public void A_held_lock_refuses_another_migrator_and_is_not_taken_over_after_the_waiters_timeout()
    {
        using var holderContext = new ProbeContext(Options);
        using var waiterContext = new ProbeContext(Options);
        var held = holderContext.GetService<IHistoryRepository>().AcquireDatabaseLock();
        var waiter = new ShortTimeoutSqliteHistory(waiterContext.GetService<HistoryRepositoryDependencies>(), SqliteDialect.Instance);

        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        // Longer than the waiter's timeout: a row-based lock would be taken over now, while its holder may still be migrating.
        Thread.Sleep(600);
        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        held.Dispose();
        using var acquired = waiter.AcquireDatabaseLock();
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);

    private sealed class ShortTimeoutSqliteHistory(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
        : SqliteHistoryRepository(dependencies, dialect)
    {
        protected override TimeSpan LockTimeout => TimeSpan.FromMilliseconds(300);
    }
}
