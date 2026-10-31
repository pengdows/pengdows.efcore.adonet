using System.Data.Common;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A database's lifecycle says where its migrations lock file is: beside a file or directory it can name, and nowhere for a server,
///     an in-memory database, or a path it can't lock beside.
/// </summary>
public class DatabaseLifecycleLockFileTests
{
    private static DbConnectionStringBuilder Connection(string text)
        => new() { ConnectionString = text };

    [Fact]
    public void A_file_database_locks_a_sidecar_file_beside_it()
    {
        var lifecycle = DatabaseLifecycle.File("Data Source");

        Assert.Equal("/data/app.db.migrations.lock", lifecycle.LockFileFor(Connection("Data Source=/data/app.db")));
    }

    [Fact]
    public void A_directory_database_locks_a_sidecar_beside_the_directory()
    {
        var lifecycle = DatabaseLifecycle.Directory("path");

        Assert.Equal("/data/store.migrations.lock", lifecycle.LockFileFor(Connection("path=/data/store/")));
    }

    [Fact]
    public void A_server_database_has_no_lock_file()
        => Assert.Null(DatabaseLifecycle.Server("Database", "master").LockFileFor(Connection("Database=app")));

    [Fact]
    public void An_in_memory_database_has_no_lock_file()
    {
        var lifecycle = DatabaseLifecycle.File("Data Source", isInMemory: b => b.ContainsKey("Data Source") && (string)b["Data Source"] == ":memory:");

        Assert.Null(lifecycle.LockFileFor(Connection("Data Source=:memory:")));
    }

    [Fact]
    public void A_path_that_cannot_be_locked_beside_has_no_lock_file()
    {
        var lifecycle = DatabaseLifecycle.File("Data Source", isLockable: b => false);

        Assert.Null(lifecycle.LockFileFor(Connection("Data Source=/data/app.db")));
    }
}
