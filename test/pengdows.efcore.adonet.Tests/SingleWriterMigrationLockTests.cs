using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Single-writer is a fact about a file-based database's storage, so it is declared on that database's lifecycle. A single-writer
///     database with no lock file to name refuses to migrate rather than run unlocked.
/// </summary>
public class SingleWriterMigrationLockTests
{
    // One dialect instance, as every dialect is: EF builds a service provider per distinct dialect, and a new one per test would trip its warning.
    private static readonly UnlockableSingleWriterDialect Dialect = new();

    [Fact]
    public void A_single_writer_lifecycle_says_so()
        => Assert.True(DatabaseLifecycle.File("Data Source", singleWriter: true).SingleWriter);

    [Fact]
    public void A_database_is_not_single_writer_unless_its_lifecycle_says_so()
        => Assert.False(DatabaseLifecycle.File("Data Source").SingleWriter);

    [Fact]
    public void A_single_writer_database_with_no_lock_file_refuses_to_migrate()
    {
        using var context = new FakeDatabase().Context(Dialect);

        var history = context.GetService<IHistoryRepository>();

        Assert.Throws<InvalidOperationException>(() => history.AcquireDatabaseLock());
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);

    // A single-writer file database whose path can't have a lock file beside it.
    private sealed class UnlockableSingleWriterDialect : AdoNetDialect
    {
        public override string Name => "Unlockable single writer";
        public override ISqlDialect Sql { get; } = new UnlockableSingleWriterSql();
    }

    private sealed class UnlockableSingleWriterSql : AnsiSqlDialect
    {
        public override DatabaseLifecycle? Databases { get; } = DatabaseLifecycle.File("Data Source", isLockable: _ => false, singleWriter: true);
    }
}
