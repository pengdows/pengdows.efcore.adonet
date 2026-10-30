using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;
using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL's bulk insert goes through MySqlConnector's MySqlBulkCopy (LOAD DATA LOCAL INFILE), not a multi-row INSERT: the
///     loader resolved for a MySQL context is the native one, and the rows it loads read back exactly.
/// </summary>
public class MySqlBulkLoaderTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [Fact]
    public void A_mysql_context_resolves_the_native_bulk_loader()
    {
        using var context = fixture.CreateContext();

        Assert.IsType<MySqlBulkLoader>(context.GetService<IAdoNetBulkLoader>());
    }

    // The native copy is taken only with AllowLoadLocalInfile=true, so the default connection string must still load (as INSERT).
    [Fact]
    public void Native_copy_is_used_only_when_the_connection_string_allows_local_load()
    {
        var connection = new MySqlConnector.MySqlConnection(fixture.ConnectionString + ";AllowLoadLocalInfile=true");

        Assert.True(MySqlBulkLoader.UsesNativeCopyFor(connection));
        Assert.False(MySqlBulkLoader.UsesNativeCopyFor(new MySqlConnector.MySqlConnection(fixture.ConnectionString)));
    }

    [Fact]
    public async Task Bulk_insert_loads_rows_that_read_back_exactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var batch = Guid.NewGuid().ToString("N");
        var rows = Enumerable.Range(1, 50).Select(i => new Blog
        {
            Name = batch + "-" + i,
            IsActive = i % 2 == 0,
            Rating = i / 4m,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5),
            ExternalId = Guid.NewGuid(),
        }).ToList();

        await using (var context = fixture.CreateContext())
        {
            await context.BulkInsertAsync(rows, ct);
        }

        await using var verify = fixture.CreateContext();
        var loaded = await verify.Blogs.AsNoTracking().Where(b => b.Name.StartsWith(batch))
            .OrderBy(b => b.Name).ToListAsync(ct);
        Assert.Equal(50, loaded.Count);
        Assert.Equal(rows.OrderBy(r => r.Name).Select(r => (r.Name, r.IsActive, r.Rating, r.ExternalId)),
            loaded.Select(r => (r.Name, r.IsActive, r.Rating, r.ExternalId)));
    }

    [Fact]
    public async Task Native_copy_loads_rows_that_read_back_exactly_when_the_connection_opts_in()
    {
        var ct = TestContext.Current.CancellationToken;
        var batch = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<BlogContext>()
            .AllowManyDialects()
            .UseAdoNetMySql(fixture.ConnectionString + ";AllowLoadLocalInfile=true")
            .Options;
        var rows = Enumerable.Range(1, 20).Select(i => new Blog
        {
            Name = batch + "-" + i,
            IsActive = i % 2 == 0,
            Rating = i / 4m,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5),
            ExternalId = Guid.NewGuid(),
        }).ToList();

        // Loading local data is off by default on the server as well as the client. This test server is disposable, so the test
        // enables it; a deployment must set local_infile on the server and AllowLoadLocalInfile on the client.
        await using (var admin = fixture.CreateContext())
        {
            await admin.Database.ExecuteSqlRawAsync("SET GLOBAL local_infile = 1", ct);
        }

        await using (var context = new BlogContext(options))
        {
            Assert.IsType<MySqlBulkLoader>(context.GetService<IAdoNetBulkLoader>());
            await context.BulkInsertAsync(rows, ct);
        }

        await using var verify = fixture.CreateContext();
        var loaded = await verify.Blogs.AsNoTracking().Where(b => b.Name.StartsWith(batch))
            .OrderBy(b => b.Name).ToListAsync(ct);
        Assert.Equal(rows.OrderBy(r => r.Name).Select(r => (r.Name, r.IsActive, r.Rating, r.ExternalId)),
            loaded.Select(r => (r.Name, r.IsActive, r.Rating, r.ExternalId)));
    }
}
