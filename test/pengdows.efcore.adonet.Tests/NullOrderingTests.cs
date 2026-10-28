using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Where nulls sort, written into each ORDER BY when the option asks for it, for a database whose own default is the
///     other way. A database that can't write the clauses refuses the option instead of producing SQL it can't run.
/// </summary>
public class NullOrderingTests
{
    private static readonly IAdoNetDialect WithClauses = new PartsDialect(
        "Null ordering clauses", new PartsSql(supportsNullOrderingClauses: true));

    private readonly FakeDatabase _db = new();

    private string Sql(IAdoNetDialect dialect, bool reverseNullOrdering, Func<ItemContext, IQueryable> query)
    {
        using var context = _db.Context(dialect, o =>
        {
            if (reverseNullOrdering)
            {
                o.ReverseNullOrdering();
            }
        });

        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    private static IQueryable TwoColumnOrder(ItemContext c)
        => c.Parts.OrderBy(p => p.Label).ThenByDescending(p => p.Id);

    [Fact]
    public void An_order_says_nothing_of_nulls_unless_asked()
        => Assert.DoesNotContain("NULLS", Sql(WithClauses, reverseNullOrdering: false, TwoColumnOrder));

    [Fact]
    public void Nulls_sort_first_ascending_and_last_descending_when_asked()
        => Assert.Contains(
            @"ORDER BY ""p"".""Label"" NULLS FIRST, ""p"".""Id"" DESC NULLS LAST",
            Sql(WithClauses, reverseNullOrdering: true, TwoColumnOrder));

    [Fact]
    public void The_setting_does_not_leak_to_a_context_without_it()
    {
        Assert.Contains("NULLS FIRST", Sql(WithClauses, reverseNullOrdering: true, TwoColumnOrder));
        Assert.DoesNotContain("NULLS", Sql(WithClauses, reverseNullOrdering: false, TwoColumnOrder));
    }

    [Fact]
    public void A_paged_order_says_where_nulls_sort()
        => Assert.Contains(
            @"ORDER BY ""p"".""Label"" NULLS FIRST",
            Sql(WithClauses, reverseNullOrdering: true, c => c.Parts.OrderBy(p => p.Label).Skip(2).Take(3)));

    [Fact]
    public void An_order_inside_a_subquery_says_where_nulls_sort()
    {
        var sql = Sql(
            WithClauses,
            reverseNullOrdering: true,
            c => c.Items.Select(i => c.Parts.Where(p => p.ItemId == i.Id).OrderByDescending(p => p.Label).First().Label));

        Assert.Contains(@"ORDER BY ""p"".""Label"" DESC NULLS LAST", sql);
    }

    [Fact]
    public void Every_order_of_a_query_with_several_says_where_nulls_sort()
    {
        var sql = Sql(
            WithClauses,
            reverseNullOrdering: true,
            c => c.Items.OrderBy(i => i.Name).Select(i => new { i.Id, First = c.Parts.OrderBy(p => p.Label).Select(p => p.Id).FirstOrDefault() }));

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "NULLS FIRST").Count);
    }

    [Fact]
    public void A_dialect_that_cannot_write_the_clauses_refuses_the_option()
    {
        var error = Assert.Throws<NotSupportedException>(() => Sql(TestDialects.Ansi, reverseNullOrdering: true, TwoColumnOrder));

        Assert.Contains("NULLS FIRST", error.Message);
        Assert.Contains(TestDialects.Ansi.Name, error.Message);
    }

    [Fact]
    public void A_dialect_that_cannot_write_the_clauses_is_fine_without_the_option()
        => Assert.DoesNotContain("NULLS", Sql(TestDialects.Ansi, reverseNullOrdering: false, TwoColumnOrder));

    [Fact]
    public void Contexts_sharing_a_service_provider_must_agree_on_the_setting()
    {
        var services = new ServiceCollection().AddEntityFrameworkAdoNet(WithClauses).BuildServiceProvider();
        DbContextOptions<ItemContext> Options(bool reverse)
            => new DbContextOptionsBuilder<ItemContext>()
                .UseAdoNet(_db.Connection, contextOwnsConnection: false, o =>
                {
                    o.UseDialect(WithClauses);
                    if (reverse)
                    {
                        o.ReverseNullOrdering();
                    }
                })
                .UseInternalServiceProvider(services)
                .Options;

        using (var first = new ItemContext(Options(reverse: true)))
        {
            Assert.Contains("NULLS FIRST", TwoColumnOrder(first).ToQueryString());
        }

        using var second = new ItemContext(Options(reverse: false));
        var error = Assert.Throws<InvalidOperationException>(() => TwoColumnOrder(second).ToQueryString());
        Assert.Contains("ReverseNullOrdering", error.Message);
    }
}
