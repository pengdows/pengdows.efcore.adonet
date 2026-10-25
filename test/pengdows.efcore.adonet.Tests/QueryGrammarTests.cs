using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Each query grammar part, written by the core for a dialect that chose it.</summary>
public class QueryGrammarTests
{
    private readonly FakeDatabase _db = new();

    private string Sql(IAdoNetDialect dialect, Func<ItemContext, IQueryable> query)
    {
        using var context = _db.Context(dialect);
        return query(context).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void Operators_are_parenthesized_only_where_precedence_needs_it()
    {
        string Where(Func<ItemContext, IQueryable> query)
            => Sql(TestDialects.Ansi, query).Split("WHERE ")[1];

        Assert.Equal(@"""i"".""Version"" % 2 = 0", Where(c => c.Items.Where(i => i.Version % 2 == 0)));
        Assert.Equal(@"(""i"".""Version"" + 1) * 2 = 4", Where(c => c.Items.Where(i => (i.Version + 1) * 2 == 4)));
        Assert.Equal(@"CAST(""i"".""Version"" AS BIGINT) - (""i"".""Big"" - 1) = 0", Where(c => c.Items.Where(i => i.Version - (i.Big - 1) == 0)));
        Assert.Equal(@"COALESCE(""i"".""Name"", '') || 'x' = 'ax'", Where(c => c.Items.Where(i => i.Name + "x" == "ax")));
        Assert.Equal(@"""i"".""Version"" > 1 AND ""i"".""Big"" IN (1, 2)", Where(c => c.Items.Where(i => i.Version > 1 && (i.Big == 1 || i.Big == 2))));
        Assert.Equal(@"""i"".""Version"" > 1 AND (""i"".""Big"" = 1 OR ""i"".""Tiny"" = 2)",
            Where(c => c.Items.Where(i => i.Version > 1 && (i.Big == 1 || i.Tiny == 2))));
        Assert.Equal(@"-(""i"".""Version"" + 1) = 0", Where(c => c.Items.Where(i => -(i.Version + 1) == 0)));
        Assert.Equal(@"""i"".""Version"" / (""i"".""Version"" / 2) = 1", Where(c => c.Items.Where(i => i.Version / (i.Version / 2) == 1)));
        Assert.Equal(@"((""i"".""Version"" & 1) | 2) ^ 4 = 0", Where(c => c.Items.Where(i => ((i.Version & 1 | 2) ^ 4) == 0)));
        Assert.Equal(@"~(""i"".""Version"" + 1) = 0", Where(c => c.Items.Where(i => ~(i.Version + 1) == 0)));
        Assert.Equal(@"CAST(""i"".""Version"" AS BIGINT) * 2 = 4", Where(c => c.Items.Where(i => (long)i.Version * 2 == 4)));
        Assert.Equal(@"""i"".""Name"" COLLATE c = 'a'", Where(c => c.Items.Where(i => EF.Functions.Collate(i.Name, "c") == "a")));
        Assert.Equal(@"NOT (""i"".""Active"") AND ""i"".""Version"" <> 2 AND ""i"".""Name"" IS NULL",
            Where(c => c.Items.Where(i => !(i.Active || i.Version == 2) && i.Name == null)));
        Assert.Equal(@"""i"".""Name"" LIKE 'a%' AND ""i"".""Version"" = 1", Where(c => c.Items.Where(i => EF.Functions.Like(i.Name!, "a%") && i.Version == 1)));
        // At equal precedence EF keeps parentheses around a non-associative operator, or a different one; an associative
        // operator nested in itself needs none.
        Assert.Equal(@"""i"".""Version"" % (""i"".""Version"" % 3) = 0", Where(c => c.Items.Where(i => i.Version % (i.Version % 3) == 0)));
        Assert.Equal(@"(""i"".""Version"" - ""i"".""Tiny"") - 1 = 0", Where(c => c.Items.Where(i => i.Version - i.Tiny - 1 == 0)));
        Assert.Equal(@"""i"".""Version"" * ""i"".""Tiny"" * 2 = 0", Where(c => c.Items.Where(i => i.Version * (i.Tiny * 2) == 0)));
        Assert.Equal(@"""i"".""Version"" + ""i"".""Tiny"" + 2 = 0", Where(c => c.Items.Where(i => i.Version + (i.Tiny + 2) == 0)));
        Assert.Equal(@"(""i"".""Version"" / 2) * 3 = 0", Where(c => c.Items.Where(i => i.Version / 2 * 3 == 0)));
        Assert.Equal(@"""i"".""Name"" IS NOT NULL OR ""i"".""Flag"" IS NULL", Where(c => c.Items.Where(i => i.Name != null || i.Flag == null)));
    }

    [Fact]
    public void A_nullable_in_subquery_can_be_written_as_exists()
    {
        var withIn = Sql(TestDialects.Ansi, c => c.Items.Where(i => !i.Parts.Select(p => p.Label).Contains("x")));
        var withExists = Sql(TestDialects.PreferExists, c => c.Items.Where(i => !i.Parts.Select(p => p.Label).Contains("x")));

        Assert.Contains("COALESCE", withIn);
        Assert.Contains("'x' IN (", withIn);
        Assert.Contains("WHERE NOT EXISTS (", withExists);
        Assert.DoesNotContain("COALESCE", withExists);
    }

    [Fact]
    public void Offset_fetch_writes_fetch_first_for_a_limit_alone()
    {
        var sql = Sql(TestDialects.Ansi, c => c.Items.OrderBy(i => i.Id).Take(3));

        Assert.Matches(@"FETCH FIRST @p\w* ROWS ONLY", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void Offset_fetch_writes_offset_alone()
    {
        var sql = Sql(TestDialects.Ansi, c => c.Items.OrderBy(i => i.Id).Skip(3));

        Assert.Matches(@"OFFSET @p\w* ROWS$", sql);
        Assert.DoesNotContain("FETCH", sql);
    }

    [Fact]
    public void Top_writes_top_for_a_limit_alone()
    {
        var sql = Sql(TestDialects.Top, c => c.Items.Take(3));

        Assert.Matches(@"(^|\n)SELECT TOP\(@p\w*\) ", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void Top_adds_an_ordering_only_when_an_offset_has_none()
    {
        var unordered = Sql(TestDialects.Top, c => c.Items.Skip(3));
        var ordered = Sql(TestDialects.Top, c => c.Items.OrderBy(i => i.Id).Skip(3).Take(2));

        Assert.Matches(@"ORDER BY \(SELECT 1\)\nOFFSET @p\w* ROWS$", unordered);
        Assert.DoesNotContain("(SELECT 1)", ordered);
        Assert.DoesNotContain("TOP", ordered);
        Assert.Matches(@"ORDER BY ""i"".""Id""\nOFFSET @p\w* ROWS FETCH NEXT @p\w* ROWS ONLY$", ordered);
    }

    [Fact]
    public void Limit_offset_writes_a_bare_offset_without_a_no_limit_value()
    {
        var sql = Sql(TestDialects.Limit, c => c.Items.OrderBy(i => i.Id).Skip(3));

        Assert.Matches(@"\nOFFSET @p\w*$", sql);
        Assert.DoesNotContain("LIMIT", sql);
    }

    [Fact]
    public void Limit_offset_writes_the_no_limit_value_for_an_offset_alone()
        => Assert.Matches(@"\nLIMIT -1\nOFFSET @p\w*$", Sql(TestDialects.LimitWithNoLimitValue, c => c.Items.OrderBy(i => i.Id).Skip(3)));

    [Fact]
    public void Limit_offset_writes_a_limit_alone()
    {
        var sql = Sql(TestDialects.Limit, c => c.Items.OrderBy(i => i.Id).Take(3));

        Assert.Matches(@"\nLIMIT @p\w*$", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void Literal_paging_inlines_values_and_varies_with_them()
    {
        var take = 3;
        var first = Sql(TestDialects.LiteralPaging, c => c.Items.OrderBy(i => i.Id).Take(take));
        take = 4;
        var second = Sql(TestDialects.LiteralPaging, c => c.Items.OrderBy(i => i.Id).Take(take));

        Assert.EndsWith("FETCH FIRST 3 ROWS ONLY", first);
        Assert.EndsWith("FETCH FIRST 4 ROWS ONLY", second);
    }

    [Fact]
    public void Native_booleans_are_used_as_predicates()
        => Assert.EndsWith("WHERE \"i\".\"Active\"", Sql(TestDialects.Ansi, c => c.Items.Where(i => i.Active)));

    [Fact]
    public void Bit_values_become_comparisons_where_a_predicate_is_needed()
    {
        Assert.EndsWith("WHERE \"i\".\"Active\" = 1", Sql(TestDialects.Bit, c => c.Items.Where(i => i.Active)));
        Assert.EndsWith("WHERE \"i\".\"Active\" = 0", Sql(TestDialects.Bit, c => c.Items.Where(i => !i.Active)));
        Assert.EndsWith(
            "WHERE \"i\".\"Active\" = 1 OR \"i\".\"Flag\" = 1",
            Sql(TestDialects.Bit, c => c.Items.Where(i => i.Active || i.Flag == true)));
    }

    [Fact]
    public void Every_comparison_used_as_a_bit_value_becomes_a_case()
    {
        string Projected(System.Linq.Expressions.Expression<Func<Item, bool>> comparison)
            => System.Text.RegularExpressions.Regex.Replace(Sql(TestDialects.Bit, c => c.Items.Select(comparison)).Split("FROM")[0], @"\s+", " ");

        Assert.Contains("CASE WHEN \"i\".\"Version\" > 1 THEN 1 ELSE 0 END", Projected(i => i.Version > 1));
        Assert.Contains("CASE WHEN \"i\".\"Version\" >= 1 THEN 1 ELSE 0 END", Projected(i => i.Version >= 1));
        Assert.Contains("CASE WHEN \"i\".\"Version\" < 1 THEN 1 ELSE 0 END", Projected(i => i.Version < 1));
        Assert.Contains("CASE WHEN \"i\".\"Version\" <= 1 THEN 1 ELSE 0 END", Projected(i => i.Version <= 1));
        Assert.Contains("CASE WHEN \"i\".\"Version\" <> 1 THEN 1 ELSE 0 END", Projected(i => i.Version != 1));
        Assert.Contains("CASE WHEN \"i\".\"Name\" IS NOT NULL THEN 1 ELSE 0 END", Projected(i => i.Name != null));
        Assert.Contains("CASE WHEN \"i\".\"Active\" = 1 AND \"i\".\"Version\" = 1 THEN 1 ELSE 0 END", Projected(i => i.Active && i.Version == 1));
    }

    [Fact]
    public void Constant_predicates_compare_integers()
    {
        Assert.EndsWith("WHERE 0 = 1", Sql(TestDialects.Bit, c => c.Items.Where(i => false)));
        Assert.EndsWith("WHERE 0 = 1", Sql(TestDialects.BitXor, c => c.Items.Where(i => i.Name == null && i.Name != null)));
    }

    [Fact]
    public void Bitwise_operators_compare_and_negate_bit_values_where_the_dialect_has_them()
    {
        Assert.Contains("SELECT ~\"i\".\"Active\"", Sql(TestDialects.BitXor, c => c.Items.Select(i => !i.Active)));
        Assert.Matches(@"SELECT ""i"".""Active"" \^ CASE", Sql(TestDialects.BitXor, c => c.Items.Select(i => i.Active != (i.Price > 5))));
        Assert.Matches(@"SELECT ~\(""i"".""Active"" \^ CASE", Sql(TestDialects.BitXor, c => c.Items.Select(i => i.Active == (i.Price > 5))));

        // Without them, a negation is a condition written as a value.
        Assert.Contains("WHEN \"i\".\"Active\" = 0 THEN 1", Sql(TestDialects.Bit, c => c.Items.Select(i => !i.Active)));
    }

    [Fact]
    public void Conditions_become_case_where_a_value_is_needed()
    {
        Assert.Contains(
            "CASE\n    WHEN \"i\".\"Price\" > 5.0 THEN 1\n    ELSE 0\nEND",
            Sql(TestDialects.Bit, c => c.Items.Select(i => i.Price > 5)));
        Assert.Contains(
            "CASE\n    WHEN \"i\".\"Name\" IS NULL THEN 1\n    ELSE 0\nEND",
            Sql(TestDialects.Bit, c => c.Items.Select(i => i.Name == null)));

        var ids = new[] { 1, 2 };
        Assert.Matches(@"CASE\n    WHEN ""i"".""Id"" IN \(", Sql(TestDialects.Bit, c => c.Items.Select(i => ids.Contains(i.Id))));
        Assert.Contains(
            "WHEN \"i\".\"Name\" LIKE 'a%'",
            Sql(TestDialects.Bit, c => c.Items.Select(i => EF.Functions.Like(i.Name!, "a%"))));
    }

    [Fact]
    public void Case_tests_are_predicates_and_case_results_are_values()
    {
        var sql = Sql(TestDialects.Bit, c => c.Items.Select(i => i.Active ? i.Price > 1 : i.Flag == true));

        Assert.Contains("WHEN \"i\".\"Active\" = 1 THEN CASE", sql);
    }

    [Fact]
    public void Orderings_are_values()
        => Assert.Contains(
            "ORDER BY CASE\n    WHEN \"i\".\"Price\" > 1.0 THEN 1\n    ELSE 0\nEND",
            Sql(TestDialects.Bit, c => c.Items.OrderBy(i => i.Price > 1)));

    [Fact]
    public void Exists_is_written_as_a_value()
        => Assert.Contains(
            "WHEN EXISTS (",
            Sql(TestDialects.Bit, c => c.Items.Select(i => i.Parts.Any())));

    [Fact]
    public void Join_predicates_are_converted_too()
    {
        Assert.Contains(
            "INNER JOIN \"Parts\" AS \"p\" ON \"i\".\"Id\" = \"p\".\"ItemId\"",
            Sql(TestDialects.Bit, c => c.Items.Join(c.Parts, i => i.Id, p => p.ItemId, (i, p) => new { i.Name, p.Label })));
        Assert.Contains(
            "LEFT JOIN \"Parts\" AS \"p\" ON \"i\".\"Id\" = \"p\".\"ItemId\"",
            Sql(TestDialects.Bit, c => c.Items.LeftJoin(c.Parts, i => i.Id, p => p.ItemId, (i, p) => new { i.Name, p!.Label })));
        Assert.Contains(
            "RIGHT JOIN \"Parts\" AS \"p\" ON \"i\".\"Id\" = \"p\".\"ItemId\"",
            Sql(TestDialects.Bit, c => c.Items.RightJoin(c.Parts, i => i.Id, p => p.ItemId, (i, p) => new { i!.Name, p.Label })));
    }

    [Fact]
    public void Long_count_uses_the_dialects_function()
    {
        Assert.Contains("COUNT_BIG(*)", Sql(TestDialects.Bit, c => c.Items.GroupBy(i => i.Name).Select(g => g.LongCount())));
        Assert.DoesNotContain("COUNT_BIG", Sql(TestDialects.Bit, c => c.Items.GroupBy(i => i.Name).Select(g => g.Count())));
        Assert.Contains("COUNT(*)", Sql(TestDialects.Ansi, c => c.Items.GroupBy(i => i.Name).Select(g => g.LongCount())));
    }

    [Fact]
    public void Parameters_use_the_dialects_marker()
    {
        var name = "x";

        Assert.EndsWith("WHERE \"i\".\"Name\" = :name", Sql(TestDialects.ColonParameters, c => c.Items.Where(i => i.Name == name)));
    }
}
