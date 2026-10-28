using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     The rest of Pomelo's MySqlDbFunctionsExtensions: MATCH over several columns and with a search mode given as a
///     parameter, HEX and UNHEX, DEGREES and RADIANS, CONVERT_TZ, and LIKE over a value that isn't a string. SQL shape only,
///     on MySQL and MariaDB, which share these functions. Provider-only: they are MySQL's. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlDbFunctionsExtensionsMethodTranslator.cs
/// </summary>
public class MySqlDbFunctionsTests
{
    public sealed class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Summary { get; set; } = "";
        public int Views { get; set; }
        public double Angle { get; set; }
        public float SmallAngle { get; set; }
        public byte[] Data { get; set; } = [];
        public DateTime PublishedAt { get; set; }
        public DateTimeOffset? EditedAt { get; set; }
        public DateOnly Day { get; set; }
    }

    private sealed class ArticleContext(DbContextOptions<ArticleContext> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();
    }

    private static ArticleContext Context(bool mariaDb)
        => new(new DbContextOptionsBuilder<ArticleContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance))
            .Options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_and_is_match_search_several_columns(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var isMatch = context.Articles
            .Where(a => EF.Functions.IsMatch(new[] { a.Title, a.Body, a.Summary }, "+cast", MySqlMatchSearchMode.Boolean))
            .ToQueryString();
        var match = context.Articles
            .Select(a => EF.Functions.Match(new[] { a.Title, a.Body }, "cast", MySqlMatchSearchMode.NaturalLanguage))
            .ToQueryString();

        Assert.Contains("MATCH (\"a\".\"Title\", \"a\".\"Body\", \"a\".\"Summary\") AGAINST ('+cast' IN BOOLEAN MODE) > 0", isMatch);
        Assert.Contains("MATCH (\"a\".\"Title\", \"a\".\"Body\") AGAINST ('cast')", match);
    }

    // A search mode known only when the query runs: each mode's MATCH, chosen by OR, since MariaDB has no MATCH inside CASE
    // and OR keeps the full-text index usable.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Is_match_with_a_search_mode_parameter_tries_each_mode(bool mariaDb)
    {
        using var context = Context(mariaDb);
        var mode = MySqlMatchSearchMode.Boolean;

        var sql = context.Articles.Where(a => EF.Functions.IsMatch(a.Title, "cast", mode)).ToQueryString();

        Assert.Contains("MATCH (\"a\".\"Title\") AGAINST ('cast') > 0", sql);
        Assert.Contains("MATCH (\"a\".\"Title\") AGAINST ('cast' WITH QUERY EXPANSION) > 0", sql);
        Assert.Contains("MATCH (\"a\".\"Title\") AGAINST ('cast' IN BOOLEAN MODE) > 0", sql);
        Assert.Contains("@mode = 2", sql);
        Assert.Contains(" OR ", sql);
    }

    // UNHEX returns bytes; Pomelo's Unhex returns a string, so the bytes are read as UTF-8 text.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hex_and_unhex(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Articles
            .Select(a => new
            {
                Text = EF.Functions.Hex(a.Title),
                Number = EF.Functions.Hex(a.Views),
                Bytes = EF.Functions.Hex(a.Data),
                Back = EF.Functions.Unhex(a.Summary)
            })
            .ToQueryString();

        Assert.Contains("HEX(\"a\".\"Title\")", sql);
        Assert.Contains("HEX(\"a\".\"Views\")", sql);
        Assert.Contains("HEX(\"a\".\"Data\")", sql);
        Assert.Contains("CONVERT(UNHEX(\"a\".\"Summary\") USING utf8mb4)", sql);
    }

    // UNHEX is null for text that isn't hexadecimal, so a null test isn't reduced to its argument's.
    [Fact]
    public void Unhex_is_null_for_text_that_is_not_hexadecimal()
    {
        using var context = Context(mariaDb: false);

        var sql = context.Articles.Where(a => EF.Functions.Unhex(a.Summary) == null).ToQueryString();

        Assert.Contains("CONVERT(UNHEX(\"a\".\"Summary\") USING utf8mb4) IS NULL", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Degrees_and_radians(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Articles
            .Select(a => new
            {
                D = EF.Functions.Degrees(a.Angle),
                R = EF.Functions.Radians(a.Angle),
                SmallD = EF.Functions.Degrees(a.SmallAngle),
                SmallR = EF.Functions.Radians(a.SmallAngle)
            })
            .ToQueryString();

        Assert.Contains("DEGREES(\"a\".\"Angle\")", sql);
        Assert.Contains("RADIANS(\"a\".\"Angle\")", sql);
        Assert.Contains("DEGREES(\"a\".\"SmallAngle\")", sql);
        Assert.Contains("RADIANS(\"a\".\"SmallAngle\")", sql);
    }

    // Without a zone to convert from, a DateTimeOffset is UTC (it is stored as its UTC instant) and anything else is in the
    // session's zone.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Convert_time_zone_from_a_zone_utc_or_the_sessions(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Articles
            .Select(a => new
            {
                Between = EF.Functions.ConvertTimeZone(a.PublishedAt, "+00:00", "Europe/Oslo"),
                FromSession = EF.Functions.ConvertTimeZone(a.PublishedAt, "Europe/Oslo"),
                FromUtc = EF.Functions.ConvertTimeZone(a.EditedAt, "Europe/Oslo"),
                Date = EF.Functions.ConvertTimeZone(a.Day, "+01:00", "+02:00")
            })
            .ToQueryString();

        Assert.Contains("CONVERT_TZ(\"a\".\"PublishedAt\", '+00:00', 'Europe/Oslo')", sql);
        Assert.Contains("CONVERT_TZ(\"a\".\"PublishedAt\", @@session.time_zone, 'Europe/Oslo')", sql);
        Assert.Contains("CONVERT_TZ(\"a\".\"EditedAt\", '+00:00', 'Europe/Oslo')", sql);
        Assert.Contains("CONVERT_TZ(\"a\".\"Day\", '+01:00', '+02:00')", sql);
    }

    // CONVERT_TZ is null for a zone the server doesn't know, so a null test isn't reduced to its arguments'.
    [Fact]
    public void Convert_time_zone_is_null_for_an_unknown_zone()
    {
        using var context = Context(mariaDb: false);

        var sql = context.Articles.Where(a => EF.Functions.ConvertTimeZone(a.PublishedAt, "Nowhere", "Europe/Oslo") == null).ToQueryString();

        Assert.Contains("CONVERT_TZ(\"a\".\"PublishedAt\", 'Nowhere', 'Europe/Oslo') IS NULL", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Like_over_a_value_that_is_not_a_string(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var plain = context.Articles.Where(a => EF.Functions.Like(a.Views, "1%")).ToQueryString();
        var escaped = context.Articles.Where(a => EF.Functions.Like(a.PublishedAt, "2024!_%", "!")).ToQueryString();

        Assert.Contains("\"a\".\"Views\" LIKE '1%'", plain);
        Assert.Contains("\"a\".\"PublishedAt\" LIKE '2024!_%' ESCAPE '!'", escaped);
    }

    // TIMESTAMPDIFF counts quarters and weeks; it has no tick or nanosecond, so microseconds are multiplied.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Date_differences_in_quarters_weeks_ticks_and_nanoseconds(bool mariaDb)
    {
        using var context = Context(mariaDb);

        var sql = context.Articles
            .Select(a => new
            {
                Q = EF.Functions.DateDiffQuarter(a.PublishedAt, a.PublishedAt),
                W = EF.Functions.DateDiffWeek(a.Day, a.Day),
                T = EF.Functions.DateDiffTick(a.PublishedAt, a.PublishedAt),
                N = EF.Functions.DateDiffNanosecond(a.EditedAt, a.EditedAt)
            })
            .ToQueryString();

        Assert.Contains("TIMESTAMPDIFF(QUARTER, \"a\".\"PublishedAt\", \"a\".\"PublishedAt\")", sql);
        Assert.Contains("TIMESTAMPDIFF(WEEK, \"a\".\"Day\", \"a\".\"Day\")", sql);
        Assert.Contains("(TIMESTAMPDIFF(MICROSECOND, \"a\".\"PublishedAt\", \"a\".\"PublishedAt\") * 10)", sql);
        Assert.Contains("(TIMESTAMPDIFF(MICROSECOND, \"a\".\"EditedAt\", \"a\".\"EditedAt\") * 1000)", sql);
    }

    [Fact]
    public void The_functions_throw_outside_a_query()
    {
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Hex(1));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.Degrees(1.0));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.ConvertTimeZone(DateTime.UtcNow, "+01:00"));
        Assert.Throws<InvalidOperationException>(() => EF.Functions.IsMatch(new[] { "a" }, "a", MySqlMatchSearchMode.Boolean));
    }
}
