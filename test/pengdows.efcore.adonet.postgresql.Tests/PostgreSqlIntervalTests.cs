using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A TimeSpan is days and time; an interval may also hold months, which have no fixed length. Such an interval is refused
///     rather than read as some other span: Npgsql reads one with negative months as TimeSpan.Zero
///     (https://github.com/npgsql/npgsql/pull/6682). Provider-only: only PostgreSQL's interval holds months.
/// </summary>
public class PostgreSqlIntervalTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    // A retrying strategy buffers each result before it's read, through another of EF's reader paths.
    private async Task<TimeSpan> ReadAsync(string interval, bool buffered = false)
    {
        await using var context = buffered
            ? fixture.CreateContext<BlogContext>(o => new BlogContext(o), o => o.EnableRetryOnFailure())
            : fixture.CreateContext();
        // The interval is a constant of the test, not input.
        var sql = "SELECT INTERVAL '" + interval + "' AS " + fixture.Quote("Value");
        return await context.Database.SqlQueryRaw<TimeSpan>(sql).SingleAsync(Ct);
    }

    [Theory]
    [InlineData("-1 month", false)]
    [InlineData("1 month", false)]
    [InlineData("-1 year 2 days", false)]
    [InlineData("-1 month", true)]
    public async Task An_interval_with_months_is_refused_not_misread(string interval, bool buffered)
        => await Assert.ThrowsAnyAsync<InvalidCastException>(() => ReadAsync(interval, buffered));

    [Theory]
    [InlineData("-3 days -04:05:06.789", "-3.04:05:06.789", false)]
    [InlineData("1000 days", "1000.00:00:00", false)]
    [InlineData("-3 days -04:05:06.789", "-3.04:05:06.789", true)]
    public async Task An_interval_of_days_and_time_reads_as_its_span(string interval, string expected, bool buffered)
        => Assert.Equal(TimeSpan.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), await ReadAsync(interval, buffered));
}
