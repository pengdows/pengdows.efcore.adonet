using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     Two migrators on one DuckDB file exclude each other while the first holds the lock, and a lock held longer than the waiter's
///     timeout is not taken over.
/// </summary>
public class DuckDbMigrationLockTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pengdows-lock-{Guid.NewGuid():N}.duckdb");

    private DbContextOptions<ProbeContext> Options
        => new DbContextOptionsBuilder<ProbeContext>().UseAdoNetDuckDb($"Data Source={_path}").Options;

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
        var waiter = new ShortTimeoutDuckDbHistory(waiterContext.GetService<HistoryRepositoryDependencies>(), DuckDbDialect.Instance);

        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        Thread.Sleep(600);
        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        held.Dispose();
        using var acquired = waiter.AcquireDatabaseLock();
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);

    private sealed class ShortTimeoutDuckDbHistory(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
        : DuckDbHistoryRepository(dependencies, dialect)
    {
        protected override TimeSpan LockTimeout => TimeSpan.FromMilliseconds(300);
    }
}
