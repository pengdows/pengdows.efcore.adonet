using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     DateTime.UtcNow is the server's clock in UTC whatever the session's time zone: DuckDB's CURRENT_TIMESTAMP is an instant shown in the session's zone, so the translation
///     names UTC. Provider-only: the session's zone is set in the database's own SQL.
/// </summary>
public class DuckDbUtcNowTests(DuckDbFixture fixture) : IClassFixture<DuckDbFixture>
{
    [Fact]
    public async Task Utc_now_is_utc_in_a_session_far_from_utc()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        await context.Database.OpenConnectionAsync(ct);
        await context.Database.ExecuteSqlRawAsync("SET TimeZone = 'Pacific/Kiritimati'", ct);

        // The predicate can't be evaluated on the client, so the database translates DateTime.UtcNow.
        var now = await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"")
            .Where(_ => DateTime.UtcNow > new DateTime(2000, 1, 1))
            .Select(_ => DateTime.UtcNow)
            .SingleAsync(ct);

        Assert.InRange(now, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(2));
    }
}
