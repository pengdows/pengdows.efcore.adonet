using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

public class DuckDbSqlTests
{
    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Uses_dollar_parameters_and_limit_offset()
    {
        using var context = Context(b => b.UseAdoNetDuckDb("Data Source=:memory:"));

        var sql = PagedQuery(context);

        Assert.Contains("= $name", sql);
        Assert.Matches(@"LIMIT \$p\w*\s+OFFSET \$p\w*", sql);
    }

    // date_diff has quarter and week parts; a tick or nanosecond difference is left untranslated.
    [Fact]
    public void Date_differences_have_quarters_and_weeks_but_no_ticks_or_nanoseconds()
    {
        var functions = DuckDbDialect.Instance.Sql.Functions;

        Assert.Equal("DATE_DIFF('quarter', {0}, {1})", functions.DateDiff(DateDiffUnit.Quarter));
        Assert.Equal("DATE_DIFF('week', {0}, {1})", functions.DateDiff(DateDiffUnit.Week));
        Assert.Null(functions.DateDiff(DateDiffUnit.Tick));
        Assert.Null(functions.DateDiff(DateDiffUnit.Nanosecond));
    }
}

/// <summary>
///     A TimeSpan is an INTERVAL here, any length and sign. Where the statement doesn't fix a parameter's type, DuckDB.NET
///     1.5.6.1 converts a DbType.Time one with TimeOnly.FromTimeSpan, which refuses a negative span or one of a day or more.
/// </summary>
public class DuckDbIntervalParameterTests(DuckDbFixture fixture) : IClassFixture<DuckDbFixture>
{
    [Theory]
    [InlineData("-1.02:03:04.5")]
    [InlineData("2.00:00:00")]
    [InlineData("00:00:01")]
    public async Task Interval_arguments_of_any_length_and_sign_read_back(string text)
    {
        var span = TimeSpan.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        await using var context = fixture.CreateContext();
        var sql = "SELECT {0} AS " + fixture.Quote("Value");

        Assert.Equal(span, await context.Database.SqlQueryRaw<TimeSpan>(sql, span).SingleAsync(TestContext.Current.CancellationToken));
    }
}
