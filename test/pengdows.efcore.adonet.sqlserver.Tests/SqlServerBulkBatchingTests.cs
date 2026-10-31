using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     The native bulk load writes its rows in batches, so a large input is never held as one table. Every row must arrive,
///     including those past a batch boundary.
/// </summary>
public class SqlServerBulkBatchingTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private const int Rows = 25_000;

    public async ValueTask InitializeAsync()
        => await fixture.ResetAsync();

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public async Task A_bulk_insert_larger_than_one_batch_stores_every_row_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var blogs = Enumerable.Range(0, Rows).Select(i => new Blog { Name = "blog" + i }).ToList();

        var loaded = await context.BulkInsertAsync(blogs, ct);

        Assert.Equal(Rows, loaded);
        Assert.Equal(Rows, Convert.ToInt32(await fixture.ExecuteScalarAsync("SELECT COUNT(*) FROM [Blogs]")));
        Assert.Equal("blog" + (Rows - 1), await fixture.ExecuteScalarAsync($"SELECT [Name] FROM [Blogs] WHERE [Id] = {Rows}"));
    }
}
