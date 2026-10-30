using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     EF.Functions.Random() is PostgreSQL's random(), a double from zero up to but not including one. EF's default translation is
///     RAND(), which PostgreSQL doesn't have.
/// </summary>
public class PostgreSqlRandomTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Random_translates_to_postgresql_random_and_returns_values_from_zero_to_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        context.Blogs.Add(new Blog { Name = "random", IsActive = true, Rating = 1, CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
        await context.SaveChangesAsync(ct);

        var values = await context.Blogs.Select(_ => EF.Functions.Random()).ToListAsync(ct);

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.InRange(value, 0d, 1d));
    }
}
