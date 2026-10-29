using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Microsoft's SpatialiteLoader, on a real SQLite connection. The SpatiaLite module (mod_spatialite) is not installed in the
///     test environment, so loading it must report that rather than succeed.
/// </summary>
public class SqliteUpstreamSpatialiteTests
{
    [Fact]
    public void TryLoad_reports_false_when_the_module_is_not_installed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        Assert.False(SpatialiteLoader.TryLoad(connection));
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void TryLoad_leaves_a_connection_the_caller_opened_open()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Assert.False(SpatialiteLoader.TryLoad(connection));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void Load_throws_when_the_module_is_not_installed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Assert.Throws<SqliteException>(() => SpatialiteLoader.Load(connection));
    }

    [Fact]
    public void Load_needs_an_open_connection()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        Assert.Throws<InvalidOperationException>(() => SpatialiteLoader.Load(connection));
    }
}
