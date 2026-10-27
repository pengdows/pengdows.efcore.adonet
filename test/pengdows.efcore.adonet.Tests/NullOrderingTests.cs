using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Where nulls sort, written into each ORDER BY when the option asks for it.</summary>
public class NullOrderingTests
{
    private readonly FakeDatabase _db = new();

    private string Sql(bool reverseNullOrdering)
    {
        using var context = _db.Context(TestDialects.Ansi, o =>
        {
            if (reverseNullOrdering)
            {
                o.ReverseNullOrdering();
            }
        });

        return context.Parts.OrderBy(p => p.Label).ThenByDescending(p => p.Id).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void An_order_says_nothing_of_nulls_unless_asked()
        => Assert.DoesNotContain("NULLS", Sql(reverseNullOrdering: false));

    [Fact]
    public void Nulls_sort_first_ascending_and_last_descending_when_asked()
        => Assert.Contains(@"ORDER BY ""p"".""Label"" NULLS FIRST, ""p"".""Id"" DESC NULLS LAST", Sql(reverseNullOrdering: true));

    [Fact]
    public void The_setting_does_not_leak_to_a_context_without_it()
    {
        Assert.Contains("NULLS FIRST", Sql(reverseNullOrdering: true));
        Assert.DoesNotContain("NULLS", Sql(reverseNullOrdering: false));
    }
}
