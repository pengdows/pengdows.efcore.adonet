using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.flatfile.Tests;

/// <summary>
///     Two migrators on one flat-file database exclude each other while the first holds the lock, and a lock held longer than the
///     waiter's timeout is not taken over.
/// </summary>
public class FlatFileMigrationLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pengdows-lock-{Guid.NewGuid():N}");

    private DbContextOptions<ProbeContext> Options
        => new DbContextOptionsBuilder<ProbeContext>().UseAdoNetFlatFile($"path={_directory}").Options;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }

        try
        {
            File.Delete(_directory + ".migrations.lock");
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void A_held_lock_refuses_another_migrator_and_is_not_taken_over_after_the_waiters_timeout()
    {
        using var holderContext = new ProbeContext(Options);
        using var waiterContext = new ProbeContext(Options);
        var held = holderContext.GetService<IHistoryRepository>().AcquireDatabaseLock();
        var waiter = new ShortTimeoutFlatFileHistory(waiterContext.GetService<HistoryRepositoryDependencies>(), FlatFileDialect.Instance);

        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        Thread.Sleep(600);
        Assert.Throws<TimeoutException>(() => waiter.AcquireDatabaseLock());

        held.Dispose();
        using var acquired = waiter.AcquireDatabaseLock();
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);

    private sealed class ShortTimeoutFlatFileHistory(HistoryRepositoryDependencies dependencies, IAdoNetDialect dialect)
        : AdoNetHistoryRepository(dependencies, dialect)
    {
        protected override TimeSpan LockTimeout => TimeSpan.FromMilliseconds(300);
    }
}
