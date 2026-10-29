using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     JSON holds a DateTimeOffset as ISO 8601 text with its offset. A database whose conversion of that text ignores the
///     offset (MariaDB's CAST and JSON_TABLE read "12:34:56-08:00" as 12:34:56) has a dialect template that applies it
///     (<see cref="JsonSyntax.DateTimeOffsetFromText" />): a value read out of a document, and an element of a JSON array
///     table, which is then declared as text and converted where it is read, as binary elements are.
/// </summary>
public class JsonDateTimeOffsetTests
{
    public class Event
    {
        public int Id { get; set; }
        public Stamp Stamp { get; set; } = new();
        public List<DateTimeOffset> Ats { get; set; } = [];
    }

    public class Stamp
    {
        public DateTimeOffset At { get; set; }
    }

    private sealed class EventContext(DbContextOptions<EventContext> options) : DbContext(options)
    {
        public DbSet<Event> Events => Set<Event>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Event>().OwnsOne(e => e.Stamp, b => b.ToJson());
    }

    private static JsonSyntax Json(string? dateTimeOffsetFromText)
        => new(
            "JSON",
            "JSON_VALUE({0}, {1})",
            "JSON_QUERY({0}, {1})",
            "JSON_VALUE({0}, {1} RETURNING {2})",
            booleanValue: null,
            binaryValue: null,
            "{0} || {1}",
            "JSON_SET({0}, {1}, {2})",
            "JSON({0})")
        {
            ArrayElements = "JSON_TABLE({0}, '$[*]' COLUMNS ({1} FOR ORDINALITY, {2} {3} PATH '$'))",
            DateTimeOffsetFromText = dateTimeOffsetFromText
        };

    private static readonly IAdoNetDialect FromText = new PartsDialect("DateTimeOffset from text", new PartsSql(json: Json("TZ_FROM_TEXT({0})")));
    private static readonly IAdoNetDialect Cast = new PartsDialect("DateTimeOffset cast", new PartsSql(json: Json(null)));

    private static string Sql(IAdoNetDialect dialect, Func<EventContext, IQueryable> query)
    {
        using var context = new FakeDatabase().ContextOf<EventContext>(dialect, o => new EventContext(o));
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    private static readonly DateTimeOffset Noon = new(2000, 1, 1, 12, 34, 56, TimeSpan.FromHours(-8));

    [Fact]
    public void A_value_in_a_document_is_converted_from_its_text()
        => Assert.Contains(
            "TZ_FROM_TEXT(JSON_VALUE(\"e\".\"Stamp\", '$.At'))",
            Sql(FromText, c => c.Events.Where(e => e.Stamp.At == Noon)));

    [Fact]
    public void An_array_element_is_read_as_text_and_converted_where_it_is_read()
    {
        var sql = Sql(FromText, c => c.Events.Where(e => e.Ats.Contains(Noon)));

        Assert.Contains("SELECT TZ_FROM_TEXT(\"a\".\"value\")", sql);
        Assert.DoesNotContain("\"value\" TIMESTAMP WITH TIME ZONE PATH", sql);
    }

    [Fact]
    public void Without_the_template_a_value_is_cast_as_before()
    {
        var sql = Sql(Cast, c => c.Events.Where(e => e.Stamp.At == Noon));

        Assert.Contains("JSON_VALUE(\"e\".\"Stamp\", '$.At' RETURNING", sql);
        Assert.DoesNotContain("TZ_FROM_TEXT", sql);
    }
}
