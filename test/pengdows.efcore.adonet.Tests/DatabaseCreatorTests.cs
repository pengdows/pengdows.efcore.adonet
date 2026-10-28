using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Creating and deleting a database as the dialect's <see cref="DatabaseLifecycle" /> says, or not at all.</summary>
public class DatabaseCreatorTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql;Database=shop";

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static (ItemContext Context, fakeDbFactory Factory, List<string> Commands) Context(DatabaseLifecycle? databases, string connectionString = ConnectionString)
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        var commands = new List<string>();
        var dialect = new PartsDialect($"Lifecycle {databases?.Storage}", new PartsSql(databases: databases));
        var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(factory, connectionString, dialect)
            .LogTo(m => commands.Add(m.ReplaceLineEndings("\n").Split('\n')[^1].Trim()), [RelationalEventId.CommandExecuted])
            .AllowManyDialects()
            .Options);
        return (context, factory, commands);
    }

    private static string? Database(string connectionString)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return builder.TryGetValue("Database", out var name) ? (string)name : null;
    }

    [Fact]
    public async Task Without_a_lifecycle_databases_are_neither_created_nor_deleted()
    {
        var (context, _, _) = Context(databases: null);
        await using var _ = context;
        var creator = context.GetService<IRelationalDatabaseCreator>();

        Assert.Contains("Lifecycle", Assert.Throws<NotSupportedException>(() => creator.Create()).Message);
        await Assert.ThrowsAsync<NotSupportedException>(() => creator.DeleteAsync(Ct));
    }

    [Fact]
    public async Task A_server_database_is_created_and_dropped_through_the_admin_database()
    {
        var lifecycle = DatabaseLifecycle.Server("Database", "postgres", dropSql: ["REVOKE CONNECT ON DATABASE {0} FROM PUBLIC", "DROP DATABASE {0}"]);
        var (context, factory, commands) = Context(lifecycle);
        await using var _ = context;
        var creator = context.GetService<IRelationalDatabaseCreator>();

        await creator.CreateAsync(Ct);
        creator.Delete();

        Assert.Equal(["CREATE DATABASE \"shop\"", "REVOKE CONNECT ON DATABASE \"shop\" FROM PUBLIC", "DROP DATABASE \"shop\""], commands);
        // The statements ran on connections to the admin database, never to the one being created or dropped.
        var databases = factory.CreatedConnections.Select(c => Database(c.ConnectionString)).ToList();
        Assert.Contains("postgres", databases);
        Assert.Equal(["shop"], databases.Where(d => d != "postgres").Distinct());
    }

    [Fact]
    public async Task A_server_admin_connection_without_a_database_leaves_the_name_out()
    {
        var (context, factory, commands) = Context(DatabaseLifecycle.Server("Database", adminDatabase: null));
        await using var _ = context;

        await context.GetService<IRelationalDatabaseCreator>().CreateAsync(Ct);

        Assert.Equal(["CREATE DATABASE \"shop\""], commands);
        Assert.Contains(factory.CreatedConnections, c => Database(c.ConnectionString) == null);
    }

    [Fact]
    public async Task A_directory_database_is_a_directory_created_once_and_deleted_once()
    {
        var path = Path.Combine(Path.GetTempPath(), "adonet-lifecycle-" + Guid.NewGuid().ToString("N"));
        var (context, _, _) = Context(DatabaseLifecycle.Directory("Data Source"), $"Data Source={path};EmulatedProduct=PostgreSql");
        await using var _ = context;
        var creator = context.GetService<IRelationalDatabaseCreator>();

        Assert.False(await creator.ExistsAsync(Ct));
        await creator.CreateAsync(Ct);
        Assert.True(Directory.Exists(path));
        Assert.Throws<InvalidOperationException>(() => creator.Create());

        await creator.DeleteAsync(Ct);
        Assert.False(Directory.Exists(path));
        Assert.Contains("does not exist", (await Assert.ThrowsAsync<InvalidOperationException>(() => creator.DeleteAsync(Ct))).Message);
    }

    // Most ADO.NET drivers clear a pool with a static ClearPool(TheirConnection) (SqlClient, Npgsql, MySqlConnector,
    // MySql.Data, Microsoft.Data.Sqlite): found by that convention, so no dialect names its driver's connection type and a
    // dialect works with any driver for its database.
    [Fact]
    public async Task A_drivers_static_ClearPool_is_found_by_convention()
    {
        PoolingConnection.Cleared = 0;
        await using var connection = new PoolingConnection { ConnectionString = ConnectionString };
        var dialect = new PartsDialect("Pooling", new PartsSql(databases: DatabaseLifecycle.Server("Database", "postgres")));
        await using var context = new ItemContext(new DbContextOptionsBuilder<ItemContext>()
            .UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(dialect))
            .AllowManyDialects()
            .Options);

        // Whether fakeDb then accepts the DROP doesn't matter here: the pool is cleared before it.
        await Record.ExceptionAsync(() => context.GetService<IRelationalDatabaseCreator>().DeleteAsync(Ct));

        Assert.Equal(1, PoolingConnection.Cleared);
    }

    /// <summary>A fakeDb connection whose type declares a driver's static ClearPool.</summary>
    private sealed class PoolingConnection : fakeDbConnection
    {
        public static int Cleared;

        public static void ClearPool(PoolingConnection connection)
            => Cleared++;
    }
}
