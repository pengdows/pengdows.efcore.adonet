using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     Raw SQL's <c>{n}</c> placeholders reach DuckDB as <c>$p0</c> references to parameters bound by name. EF's own builder
///     writes the bare name, which DuckDB reads as a column; these run the statements on DuckDB itself.
/// </summary>
public class DuckDbRawSqlTests(DuckDbFixture fixture) : IClassFixture<DuckDbFixture>
{
    [Fact]
    public async Task Sql_query_placeholders_bind_their_values()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();

        var value = await context.Database.SqlQueryRaw<string>("SELECT {0} AS \"Value\"", "abc").SingleAsync(ct);

        Assert.Equal("abc", value);
    }

    [Fact]
    public async Task Execute_sql_placeholders_update_the_rows_they_match()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid().ToString("N")[..8];
        await using (var seed = fixture.CreateContext())
        {
            seed.Add(new Blog { Name = "before " + id, IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
            await seed.SaveChangesAsync(ct);
        }

        await using var context = fixture.CreateContext();
        var affected = await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"Blogs\" SET \"Name\" = {0} WHERE \"Name\" = {1}", ["after " + id, "before " + id], ct);

        Assert.Equal(1, affected);
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Blogs.CountAsync(b => b.Name == "after " + id, ct));
    }
}
