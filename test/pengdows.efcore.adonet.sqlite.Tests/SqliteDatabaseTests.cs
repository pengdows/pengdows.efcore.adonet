using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     What creating and deleting a SQLite database does beyond the shared <c>DatabaseCreatorConformanceTests</c>: the file is
///     made in write-ahead-log mode, and a database in memory or still open elsewhere is deleted all the same.
/// </summary>
/// <remarks>
///     Covers Microsoft's <c>SqliteDatabaseCreatorTest</c> (<c>Create_sets_journal_mode_to_wal</c>,
///     <c>Delete_works_for_in_memory_database</c>, <c>Delete_works_even_when_different_connection_exists_to_same_file</c>).
///     Runs on: SQLite (provider-only: journal modes, in-memory databases and file locks are SQLite's). Behaviour reference
///     (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/SqliteDatabaseCreatorTest.cs
/// </remarks>
public class SqliteDatabaseTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static string NewFile()
        => Path.Combine(Path.GetTempPath(), $"adonet-sqlite-{Guid.NewGuid():N}.db");

    private static BlogContext Context(string connectionString)
        => new(new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlite(connectionString).Options);

    [Fact]
    public async Task A_created_database_keeps_a_write_ahead_log()
    {
        var path = NewFile();
        await using (var context = Context($"Data Source={path}"))
        {
            await context.Database.EnsureCreatedAsync(Ct);
        }

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        Assert.Equal("wal", (string?)await command.ExecuteScalarAsync(Ct));

        await connection.CloseAsync();
        await using var cleanup = Context($"Data Source={path}");
        await cleanup.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task A_database_still_open_elsewhere_is_deleted_with_its_companion_files()
    {
        var path = NewFile();
        await using var context = Context($"Data Source={path}");
        await context.Database.EnsureCreatedAsync(Ct);
        await using var other = new SqliteConnection($"Data Source={path}");
        await other.OpenAsync(Ct);

        Assert.True(await context.Database.EnsureDeletedAsync(Ct));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
    }

    [Fact]
    public async Task A_database_in_memory_always_exists_and_deleting_it_succeeds()
    {
        await using var context = Context("Data Source=:memory:");

        Assert.True(await context.Database.EnsureCreatedAsync(Ct));
        Assert.True(await context.Database.EnsureDeletedAsync(Ct));
    }
}
