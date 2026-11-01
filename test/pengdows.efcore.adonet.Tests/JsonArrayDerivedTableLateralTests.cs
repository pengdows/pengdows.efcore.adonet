using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     An array whose elements are a derived table over the JSON (PostgreSQL's jsonb_array_elements in a subquery) refers to the
///     row it belongs to, so the join that reads it is a LATERAL join. A table function such as JSON_TABLE sees the earlier tables
///     without the keyword, and keeps it.
/// </summary>
public class JsonArrayDerivedTableLateralTests
{
    public class Event
    {
        public int Id { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class EventContext(DbContextOptions<EventContext> options) : DbContext(options)
    {
        public DbSet<Event> Events => Set<Event>();
    }

    private static JsonSyntax Json(string arrayElements)
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
            ArrayElements = arrayElements
        };

    private static readonly IAdoNetDialect Derived = new PartsDialect(
        "Derived elements",
        new PartsSql(json: Json("(SELECT e.value AS {2} FROM jsonb_array_elements({0}) AS e(value))")));

    private static readonly IAdoNetDialect TableFunction = new PartsDialect(
        "Table function elements",
        new PartsSql(json: Json("JSON_TABLE({0}, '$[*]' COLUMNS ({1} FOR ORDINALITY, {2} {3} PATH '$'))")));

    private static string Sql(IAdoNetDialect dialect)
    {
        using var context = new FakeDatabase().ContextOf<EventContext>(dialect, o => new EventContext(o));
        return context.Events.SelectMany(e => e.Tags).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void An_array_written_as_a_derived_table_is_joined_laterally()
        => Assert.Contains("LATERAL (SELECT", Sql(Derived), StringComparison.Ordinal);

    [Fact]
    public void A_table_function_array_keeps_its_join_without_the_keyword()
        => Assert.DoesNotContain("LATERAL", Sql(TableFunction), StringComparison.Ordinal);
}
