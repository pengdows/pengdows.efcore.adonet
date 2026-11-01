using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A dialect whose OFFSET takes only a constant or a parameter (DuckDB refuses a correlated one) must not write an ElementAt
///     whose index is a column as OFFSET. The collection's ordinal, when it has one, selects the position instead.
/// </summary>
public class ValueOnlyOffsetElementAtTests
{
    public class Row
    {
        public int Id { get; set; }
    }

    private sealed class RowContext(DbContextOptions<RowContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    private static readonly IAdoNetDialect ValueOffsets = new PartsDialect(
        "Value-only offsets",
        new PartsSql(paging: PagingSyntax.LimitOffset().WithValueLimitsOnly()));

    private static string Sql(Func<RowContext, IQueryable<string?>> query)
    {
        using var context = new FakeDatabase().ContextOf<RowContext>(ValueOffsets, o => new RowContext(o));
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    // ElementAtOrDefault's guard writes the index as the OFFSET; a dialect whose OFFSET takes only values selects by the ordinal.
    [Fact]
    public void An_inline_collection_indexed_by_a_column_selects_by_its_ordinal_not_an_offset()
    {
        var sql = Sql(c => c.Rows.Select(r => new[] { "one", "two" }.ElementAtOrDefault(r.Id % 2)));

        Assert.DoesNotContain("OFFSET", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_parameter_collection_indexed_by_a_column_selects_by_its_ordinal_not_an_offset()
    {
        var values = new List<string> { "one", "two" };

        var sql = Sql(c => c.Rows.Select(r => values.ElementAtOrDefault(r.Id % 2)));

        Assert.DoesNotContain("OFFSET", sql, StringComparison.Ordinal);
    }
}
