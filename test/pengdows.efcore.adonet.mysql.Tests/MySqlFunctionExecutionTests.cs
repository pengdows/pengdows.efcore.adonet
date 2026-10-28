using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL's EF.Functions run through fakeDb (emulating MySQL, and MariaDB where the SQL differs): the command and parameters
///     actually sent, and the values read back from scripted rows, for the JSON functions, full-text MATCH, HEX/UNHEX,
///     DEGREES/RADIANS, CONVERT_TZ, LIKE over any value and the DateDiff units. The SQL-shape tests stay beside these
///     (MySqlJsonFunctionsTests, MySqlDbFunctionsTests). Provider-only: these are MySQL's functions.
/// </summary>
public class MySqlFunctionExecutionTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class Address
    {
        public string City { get; set; } = "";
    }

    public sealed class Article
    {
        public int Id { get; set; }
        public string Doc { get; set; } = "{}";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Summary { get; set; } = "";
        public int Views { get; set; }
        public double Angle { get; set; }
        public float SmallAngle { get; set; }
        public byte[] Data { get; set; } = [];
        public DateTime PublishedAt { get; set; }
        public DateTimeOffset EditedAt { get; set; }
        public DateOnly Day { get; set; }
    }

    public sealed class ArticleContext(DbContextOptions<ArticleContext> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Article>().Property(a => a.Doc).HasColumnType("json");
    }

    private static (FakeMySql Db, ArticleContext Context) Fake(bool mariaDb = false)
    {
        var db = new FakeMySql();
        return (db, db.Context<ArticleContext>(o => new ArticleContext(o), mariaDb));
    }

    // A candidate given at run time is a parameter, sent as JSON text; AsJson makes it JSON as the dialect can.
    [Theory]
    [InlineData(false, "CAST(@json AS JSON)")]
    [InlineData(true, "JSON_COMPACT(@json)")]
    public async Task Json_contains_sends_its_candidate_and_reads_the_matching_rows(bool mariaDb, string asJson)
    {
        var (db, context) = Fake(mariaDb);
        await using var _ = context;
        var json = "[1, 2]";
        db.Returns([("Id", 3)], [("Id", 7)]);

        var ids = await context.Articles.Where(a => EF.Functions.JsonContains(a.Doc, EF.Functions.AsJson(json))).Select(a => a.Id).ToListAsync(Ct);

        Assert.Equal([3, 7], ids);
        Assert.Contains("JSON_CONTAINS(\"a\".\"Doc\", " + asJson + ")", db.Commands.Single());
        Assert.Contains("@json='[1, 2]'", db.Parameters.Single());
    }

    [Fact]
    public async Task Json_type_quote_and_unquote_read_back_as_text()
    {
        var (db, context) = Fake();
        await using var _ = context;
        db.Returns([("Type", "OBJECT"), ("Quoted", "\"Bob\""), ("Unquoted", "Bob")]);

        var row = await context.Articles
            .Select(a => new { Type = EF.Functions.JsonType(a.Doc), Quoted = EF.Functions.JsonQuote(a.Title), Unquoted = EF.Functions.JsonUnquote(a.Doc) })
            .SingleAsync(Ct);

        Assert.Equal(("OBJECT", "\"Bob\"", "Bob"), (row.Type, row.Quoted, row.Unquoted));
        Assert.Contains("JSON_TYPE(\"a\".\"Doc\")", db.Commands.Single());
        Assert.Contains("JSON_QUOTE(\"a\".\"Title\")", db.Commands.Single());
        Assert.Contains("JSON_UNQUOTE(\"a\".\"Doc\")", db.Commands.Single());
    }

    // The server returns JSON text for a string, an object or an array, and the scalar's own value through its CAST. The
    // CAST is a BIGINT, which MySqlConnector reads as an int; fakeDb's reader doesn't narrow one (UPSTREAM_ISSUES.md), so
    // the int is scripted.
    [Fact]
    public async Task Json_extract_reads_text_scalars_objects_and_arrays()
    {
        var (db, context) = Fake();
        await using var _ = context;
        db.Returns([("Text", "\"Oslo\""), ("Number", 42),("Address", "{\"City\": \"Oslo\"}"), ("Numbers", "[1, 2]")]);

        var row = await context.Articles
            .Select(a => new
            {
                Text = EF.Functions.JsonExtract<string>(a.Doc, "$.city"),
                Number = EF.Functions.JsonExtract<int>(a.Doc, "$.n"),
                Address = EF.Functions.JsonExtract<Address>(a.Doc, "$.address"),
                Numbers = EF.Functions.JsonExtract<int[]>(a.Doc, "$.a", "$.b")
            })
            .SingleAsync(Ct);

        Assert.Equal("\"Oslo\"", row.Text);
        Assert.Equal(42, row.Number);
        Assert.Equal("Oslo", row.Address.City);
        Assert.Equal([1, 2], row.Numbers);
        Assert.Contains("JSON_EXTRACT(\"a\".\"Doc\", '$.a', '$.b')", db.Commands.Single());
        Assert.Contains("AS SIGNED)", db.Commands.Single());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Json_predicates_send_their_paths_and_patterns(bool mariaDb)
    {
        var (db, context) = Fake(mariaDb);
        await using var _ = context;
        var pattern = "Bob%";
        db.Returns([("Id", 5)]);

        var ids = await context.Articles
            .Where(a => EF.Functions.JsonOverlaps(a.Doc, "[1]")
                && EF.Functions.JsonContainsPath(a.Doc, "$.a")
                && EF.Functions.JsonContainsPathAny(a.Doc, "$.a", "$.b")
                && EF.Functions.JsonContainsPathAll(a.Doc, "$.a", "$.b")
                && EF.Functions.JsonSearchAny(a.Doc, pattern)
                && EF.Functions.JsonSearchAny(a.Doc, pattern, "$.names")
                && EF.Functions.JsonSearchAny(a.Doc, pattern, "$.names", "|"))
            .Select(a => a.Id)
            .ToListAsync(Ct);

        var sql = db.Commands.Single();
        Assert.Equal([5], ids);
        Assert.Contains("JSON_OVERLAPS(\"a\".\"Doc\", '[1]')", sql);
        Assert.Contains("JSON_CONTAINS_PATH(\"a\".\"Doc\", 'one', '$.a')", sql);
        Assert.Contains("JSON_CONTAINS_PATH(\"a\".\"Doc\", 'all', '$.a', '$.b')", sql);
        Assert.Contains("JSON_SEARCH(\"a\".\"Doc\", 'one', @pattern) IS NOT NULL", sql);
        Assert.Contains("JSON_SEARCH(\"a\".\"Doc\", 'one', @pattern, '|', '$.names') IS NOT NULL", sql);
        Assert.Contains("@pattern='Bob%'", db.Parameters.Single());
    }

    [Fact]
    public async Task Match_reads_its_relevance_and_is_match_its_truth()
    {
        var (db, context) = Fake();
        await using var _ = context;
        var mode = MySqlMatchSearchMode.Boolean;
        db.Returns([("Relevance", 0.75), ("Matches", true)]);
        db.Returns([("Id", 9)]);

        var row = await context.Articles
            .Select(a => new
            {
                Relevance = EF.Functions.Match(new[] { a.Title, a.Body }, "cast", MySqlMatchSearchMode.NaturalLanguage),
                Matches = EF.Functions.IsMatch(a.Title, "cast", MySqlMatchSearchMode.Boolean)
            })
            .SingleAsync(Ct);
        var ids = await context.Articles.Where(a => EF.Functions.IsMatch(new[] { a.Title, a.Body }, "+cast", mode)).Select(a => a.Id).ToListAsync(Ct);

        Assert.Equal(0.75, row.Relevance);
        Assert.True(row.Matches);
        Assert.Equal([9], ids);
        Assert.Contains("MATCH (\"a\".\"Title\", \"a\".\"Body\") AGAINST ('cast')", db.Commands[0]);
        Assert.Contains("MATCH (\"a\".\"Title\", \"a\".\"Body\") AGAINST ('+cast' IN BOOLEAN MODE) > 0", db.Commands[1]);
        Assert.Contains("@mode='2'", db.Parameters[1]);
    }

    [Fact]
    public async Task Hex_and_unhex_read_back_as_text()
    {
        var (db, context) = Fake();
        await using var _ = context;
        db.Returns([("Text", "414243"), ("Number", "2A"), ("Bytes", "0102"), ("Back", "ABC")]);

        var row = await context.Articles
            .Select(a => new
            {
                Text = EF.Functions.Hex(a.Title),
                Number = EF.Functions.Hex(a.Views),
                Bytes = EF.Functions.Hex(a.Data),
                Back = EF.Functions.Unhex(a.Summary)
            })
            .SingleAsync(Ct);

        Assert.Equal(("414243", "2A", "0102", "ABC"), (row.Text, row.Number, row.Bytes, row.Back));
        Assert.Contains("CONVERT(UNHEX(\"a\".\"Summary\") USING utf8mb4)", db.Commands.Single());
    }

    [Fact]
    public async Task Degrees_and_radians_read_back_as_double_and_float()
    {
        var (db, context) = Fake();
        await using var _ = context;
        db.Returns([("D", 180.0), ("R", 3.5), ("SmallD", 90.0f), ("SmallR", 1.5f)]);

        var row = await context.Articles
            .Select(a => new
            {
                D = EF.Functions.Degrees(a.Angle),
                R = EF.Functions.Radians(a.Angle),
                SmallD = EF.Functions.Degrees(a.SmallAngle),
                SmallR = EF.Functions.Radians(a.SmallAngle)
            })
            .SingleAsync(Ct);

        Assert.Equal((180.0, 3.5, 90.0f, 1.5f), (row.D, row.R, row.SmallD, row.SmallR));
        Assert.Contains("DEGREES(\"a\".\"SmallAngle\")", db.Commands.Single());
    }

    // A zone known at run time is a parameter; the result is a DateTime (or DateOnly), null for a zone the server lacks.
    [Fact]
    public async Task Convert_time_zone_sends_its_zones_and_reads_back_a_date_and_time()
    {
        var (db, context) = Fake();
        await using var _ = context;
        var zone = "Europe/Oslo";
        db.Returns([("Between", new DateTime(2024, 3, 5, 7, 0, 0)), ("FromUtc", new DateTime(2024, 3, 5, 8, 0, 0)), ("Date", null)]);

        var row = await context.Articles
            .Select(a => new
            {
                Between = EF.Functions.ConvertTimeZone(a.PublishedAt, "+00:00", zone),
                FromUtc = EF.Functions.ConvertTimeZone(a.EditedAt, zone),
                Date = EF.Functions.ConvertTimeZone(a.Day, "Nowhere", zone)
            })
            .SingleAsync(Ct);

        Assert.Equal(new DateTime(2024, 3, 5, 7, 0, 0), row.Between);
        Assert.Equal(new DateTime(2024, 3, 5, 8, 0, 0), row.FromUtc);
        Assert.Null(row.Date);
        Assert.Contains("CONVERT_TZ(\"a\".\"EditedAt\", '+00:00', @zone)", db.Commands.Single());
        Assert.Contains("@zone='Europe/Oslo'", db.Parameters.Single());
    }

    [Fact]
    public async Task Like_over_a_number_sends_its_pattern()
    {
        var (db, context) = Fake();
        await using var _ = context;
        var pattern = "1%";
        db.Returns([("Id", 1)], [("Id", 2)]);

        var ids = await context.Articles.Where(a => EF.Functions.Like(a.Views, pattern)).Select(a => a.Id).ToListAsync(Ct);

        Assert.Equal([1, 2], ids);
        Assert.Contains("\"a\".\"Views\" LIKE @pattern", db.Commands.Single());
        Assert.Contains("@pattern='1%'", db.Parameters.Single());
    }

    [Fact]
    public async Task Date_differences_read_back_as_integers()
    {
        var (db, context) = Fake();
        await using var _ = context;
        db.Returns([("Q", 4), ("W", 52), ("T", 10_000_000), ("N", 1_000_000_000)]);

        var row = await context.Articles
            .Select(a => new
            {
                Q = EF.Functions.DateDiffQuarter(a.PublishedAt, a.PublishedAt),
                W = EF.Functions.DateDiffWeek(a.Day, a.Day),
                T = EF.Functions.DateDiffTick(a.PublishedAt, a.PublishedAt),
                N = EF.Functions.DateDiffNanosecond(a.EditedAt, a.EditedAt)
            })
            .SingleAsync(Ct);

        Assert.Equal((4, 52, 10_000_000, 1_000_000_000), (row.Q, row.W, row.T, row.N));
        Assert.Contains("(TIMESTAMPDIFF(MICROSECOND, \"a\".\"PublishedAt\", \"a\".\"PublishedAt\") * 10)", db.Commands.Single());
    }
}
