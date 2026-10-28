using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>ExecuteDelete/ExecuteUpdate as each <see cref="DmlSyntax" /> writes them.</summary>
public class DmlGrammarTests
{
    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private async Task<string> Delete(IAdoNetDialect dialect, Func<ItemContext, IQueryable<object>> query)
    {
        await using var context = _db.Context(dialect);
        await query(context).ExecuteDeleteAsync(Ct);
        return _db.Commands[^1];
    }

    private async Task<string> DeleteExpensive(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Items.Where(i => i.Price > 1).ExecuteDeleteAsync(Ct);
        return _db.Commands[^1];
    }

    private async Task<string> DeletePartsOfInactiveItems(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Parts.Where(p => !p.Item.Active).ExecuteDeleteAsync(Ct);
        return _db.Commands[^1];
    }

    private async Task<string> DeleteOrdered(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Items.OrderBy(i => i.Id).ExecuteDeleteAsync(Ct);
        return _db.Commands[^1];
    }

    private async Task<string> DeleteDistinct(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Items.Select(i => i).Distinct().ExecuteDeleteAsync(Ct);
        return _db.Commands[^1];
    }

    private async Task<string> RenameActive(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Items.Where(i => i.Active).ExecuteUpdateAsync(s => s.SetProperty(i => i.Name, "x").SetProperty(i => i.Tiny, 1), Ct);
        return _db.Commands[^1];
    }

    private async Task<string> RelabelPartsOfActiveItems(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Parts.Where(p => p.Item.Active).ExecuteUpdateAsync(s => s.SetProperty(p => p.Label, "x"), Ct);
        return _db.Commands[^1];
    }

    private async Task<string> CopyItemNamesToPartsOfActiveItems(IAdoNetDialect dialect)
    {
        await using var context = _db.Context(dialect);
        await context.Parts.Join(context.Items, p => p.ItemId, i => i.Id, (p, i) => new { Part = p, Item = i })
            .Where(x => x.Item.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Part.Label, x => x.Item.Name), Ct);
        return _db.Commands[^1];
    }

    // ISO SQL:2003 has no UPDATE ... FROM: an UPDATE through a join keeps its target alone, the joined tables in a correlated
    // EXISTS, and a value read from them in a correlated scalar subquery. The target takes its correlation name (SQL:2003).
    [Fact]
    public async Task Iso_updates_through_a_join_with_correlated_subqueries()
    {
        var relabel = await RelabelPartsOfActiveItems(TestDialects.Iso);
        Assert.Matches(@"^UPDATE ""Parts"" AS ""p""\nSET ""Label"" = @p\nWHERE EXISTS \(SELECT 1 FROM ""Items"" AS ""i"" WHERE ", relabel);
        Assert.Contains(@"""p"".""ItemId"" = ""i"".""Id""", relabel);
        Assert.Contains(@"""i"".""Active""", relabel);

        var copy = await CopyItemNamesToPartsOfActiveItems(TestDialects.Iso);
        Assert.Matches(@"^UPDATE ""Parts"" AS ""p""\nSET ""Label"" = \(SELECT ""i"".""Name"" FROM ""Items"" AS ""i"" WHERE ", copy);
        Assert.Contains("\nWHERE EXISTS (SELECT 1 FROM \"Items\" AS \"i\" WHERE ", copy);

        Assert.StartsWith("UPDATE \"Items\" AS \"i\"\nSET \"Name\" = @p,", await RenameActive(TestDialects.Iso));
        Assert.Matches(@"^DELETE FROM ""Parts"" AS ""p""\nWHERE ""p"".""Id"" IN \(", await DeletePartsOfInactiveItems(TestDialects.Iso));
    }

    [Fact]
    public async Task Standard_aliases_the_target_and_uses_a_subquery_for_joins()
    {
        Assert.Equal("DELETE FROM \"Items\" AS \"i\"\nWHERE \"i\".\"Price\" > 1.0", await DeleteExpensive(TestDialects.Ansi));
        Assert.Matches(@"^DELETE FROM ""Parts"" AS ""p""\nWHERE ""p"".""Id"" IN \(", await DeletePartsOfInactiveItems(TestDialects.Ansi));
        Assert.StartsWith("UPDATE \"Items\" AS \"i\"\nSET \"Name\" = @p,", await RenameActive(TestDialects.Ansi));
        Assert.Matches(@"^UPDATE ""Parts"" AS ""p""\nSET ""Label"" = @p\nFROM ""Items"" AS ""i""\nWHERE", await RelabelPartsOfActiveItems(TestDialects.Ansi));
    }

    [Fact]
    public async Task Unaliased_target_qualifies_its_columns_by_table_name()
    {
        Assert.Equal("DELETE FROM \"Items\"\nWHERE \"Items\".\"Price\" > 1.0", await DeleteExpensive(TestDialects.UnaliasedTarget));
        Assert.Equal(
            "UPDATE \"Items\"\nSET \"Name\" = @p, \"Tiny\" = CAST(@p1 AS SMALLINT)\nWHERE \"Items\".\"Active\"",
            await RenameActive(TestDialects.UnaliasedTarget));
        Assert.Matches(
            @"^DELETE FROM ""Parts""\nWHERE ""Parts"".""Id"" IN \(\n    SELECT ""p0"".""Id""\n    FROM ""Parts"" AS ""p0""",
            await DeletePartsOfInactiveItems(TestDialects.UnaliasedTarget));
    }

    [Fact]
    public async Task Unaliased_target_cannot_update_through_a_join()
        => Assert.Contains(
            "target alias",
            (await Assert.ThrowsAsync<NotSupportedException>(() => RelabelPartsOfActiveItems(TestDialects.UnaliasedTarget))).Message);

    [Fact]
    public async Task Target_alias_from_names_the_alias_then_lists_the_tables()
    {
        Assert.Equal("DELETE FROM \"i\"\nFROM \"Items\" AS \"i\"\nWHERE \"i\".\"Price\" > 1.0", await DeleteExpensive(TestDialects.TargetAliasFrom));
        Assert.Equal(
            "DELETE FROM \"p\"\nFROM \"Parts\" AS \"p\"\nINNER JOIN \"Items\" AS \"i\" ON \"p\".\"ItemId\" = \"i\".\"Id\"\nWHERE NOT (\"i\".\"Active\")",
            await DeletePartsOfInactiveItems(TestDialects.TargetAliasFrom));
        Assert.Equal(
            "UPDATE \"i\"\nSET \"i\".\"Name\" = @p, \"i\".\"Tiny\" = CAST(@p1 AS SMALLINT)\nFROM \"Items\" AS \"i\"\nWHERE \"i\".\"Active\"",
            await RenameActive(TestDialects.TargetAliasFrom));
        Assert.Equal(
            "UPDATE \"p\"\nSET \"p\".\"Label\" = @p\nFROM \"Parts\" AS \"p\"\nINNER JOIN \"Items\" AS \"i\" ON \"p\".\"ItemId\" = \"i\".\"Id\"\nWHERE \"i\".\"Active\"",
            await RelabelPartsOfActiveItems(TestDialects.TargetAliasFrom));
    }

    [Fact]
    public async Task Multi_table_deletes_by_alias_and_updates_the_joined_tables()
    {
        Assert.Equal("DELETE \"i\"\nFROM \"Items\" AS \"i\"\nWHERE \"i\".\"Price\" > 1.0", await DeleteExpensive(TestDialects.MultiTable));
        Assert.Equal(
            "DELETE \"p\"\nFROM \"Parts\" AS \"p\"\nINNER JOIN \"Items\" AS \"i\" ON \"p\".\"ItemId\" = \"i\".\"Id\"\nWHERE NOT (\"i\".\"Active\")",
            await DeletePartsOfInactiveItems(TestDialects.MultiTable));
        Assert.Equal(
            "UPDATE \"Items\" AS \"i\"\nSET \"i\".\"Name\" = @p, \"i\".\"Tiny\" = CAST(@p1 AS SMALLINT)\nWHERE \"i\".\"Active\"",
            await RenameActive(TestDialects.MultiTable));
        Assert.Equal(
            "UPDATE \"Parts\" AS \"p\"\nINNER JOIN \"Items\" AS \"i\" ON \"p\".\"ItemId\" = \"i\".\"Id\"\nSET \"p\".\"Label\" = @p\nWHERE \"i\".\"Active\"",
            await RelabelPartsOfActiveItems(TestDialects.MultiTable));
    }

    [Fact]
    public async Task Multi_table_single_table_deletes_ignore_order_and_distinct()
    {
        Assert.Equal("DELETE \"i\"\nFROM \"Items\" AS \"i\"", await DeleteOrdered(TestDialects.MultiTable));
        Assert.Equal("DELETE \"i\"\nFROM \"Items\" AS \"i\"", await DeleteDistinct(TestDialects.MultiTable));
    }

    [Fact]
    public async Task Joined_forms_still_fall_back_to_a_subquery_for_what_they_cannot_write()
    {
        var sql = await Delete(TestDialects.TargetAliasFrom, c => c.Items.OrderBy(i => i.Id).Take(2));

        Assert.Matches(@"^DELETE FROM ""i""\nFROM ""Items"" AS ""i""\nWHERE ""i"".""Id"" IN \(", sql);
    }

    [Fact]
    public async Task Bit_values_are_converted_inside_bulk_statements()
    {
        await using var context = _db.Context(TestDialects.Bit);

        await context.Items.Where(i => i.Active).ExecuteUpdateAsync(s => s.SetProperty(i => i.Active, i => i.Price > 1), Ct);

        Assert.Equal(
            "UPDATE \"Items\" AS \"i\"\nSET \"Active\" = CASE\n    WHEN \"i\".\"Price\" > 1.0 THEN 1\n    ELSE 0\nEND\nWHERE \"i\".\"Active\" = 1",
            _db.Commands[^1]);
    }
}
