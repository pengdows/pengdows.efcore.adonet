using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A single-writer database (one writer at a time, any number of readers) must coordinate its migrations through a lock. A
///     single-writer dialect with no lock file to name must refuse to migrate rather than run without a lock.
/// </summary>
public class SingleWriterMigrationLockTests
{
    [Fact]
    public void A_single_writer_dialect_with_no_lock_file_refuses_to_migrate()
    {
        var dialect = new PartsDialect("Single writer", capabilities: new DatabaseCapabilities { SingleWriter = true });
        using var context = new FakeDatabase().Context(dialect);

        var history = context.GetService<IHistoryRepository>();

        Assert.Throws<InvalidOperationException>(() => history.AcquireDatabaseLock());
    }
}
