using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Tuples compared in a query become ISO row values, <c>(a, b) = (x, y)</c>, where the dialect has them. Nullable members
///     are compared one by one where SQL's NULL would differ from .NET's tuple equality.
/// </summary>
public class RowValueTests
{
    private readonly FakeDatabase _db = new();

    private string Where(IAdoNetDialect dialect, Func<ItemContext, IQueryable> query)
    {
        using var context = _db.Context(dialect);
        return query(context).ToQueryString().ReplaceLineEndings("\n").Split("WHERE ")[1];
    }

    [Fact]
    public void Constructed_tuples_compare_as_row_values()
    {
        Assert.Equal(@"(""i"".""Version"", ""i"".""Tiny"") = (1, 2)",
            Where(TestDialects.Ansi, c => c.Items.Where(i => new Tuple<int, byte>(i.Version, i.Tiny).Equals(new Tuple<int, byte>(1, 2)))));
        Assert.Equal(@"(""i"".""Version"") = (1)",
            Where(TestDialects.Ansi, c => c.Items.Where(i => new Tuple<int>(i.Version).Equals(new Tuple<int>(1)))));
        Assert.Equal(@"(""i"".""Version"", ""i"".""Tiny"") <> (1, 2)",
            Where(TestDialects.Ansi, c => c.Items.Where(i => !new Tuple<int, byte>(i.Version, i.Tiny).Equals(new Tuple<int, byte>(1, 2)))));
    }

    [Fact]
    public void Value_tuples_compare_as_row_values()
        => Assert.Equal(@"(""i"".""Version"", ""i"".""Tiny"") = (1, 2)",
            Where(TestDialects.Ansi, c => c.Items.Where(i => ValueTuple.Create(i.Version, i.Tiny).Equals(ValueTuple.Create(1, (byte)2)))));

    [Fact]
    public void Nullable_members_are_compared_with_dotnet_null_semantics()
    {
        // Equality in a WHERE: a NULL name fails both, as in .NET.
        Assert.Equal(@"(""i"".""Name"", ""i"".""Version"") = ('a', 1)",
            Where(TestDialects.Ansi, c => c.Items.Where(i => new Tuple<string?, int>(i.Name, i.Version).Equals(new Tuple<string?, int>("a", 1)))));

        // Inequality: a NULL name is unequal to 'a' in .NET, which SQL's <> alone would drop. Pairs that stay compared as a
        // row come first.
        Assert.Equal(@"""i"".""Version"" <> 1 OR ""i"".""Name"" <> 'a' OR ""i"".""Name"" IS NULL",
            Where(TestDialects.Ansi, c => c.Items.Where(i => !new Tuple<string?, int>(i.Name, i.Version).Equals(new Tuple<string?, int>("a", 1)))));
    }

    [Fact]
    public void Without_row_values_tuples_are_not_translated()
    {
        using var context = _db.Context(TestDialects.NoRowValues);

        Assert.Contains("could not be translated", Assert.Throws<InvalidOperationException>(
            () => context.Items.Where(i => new Tuple<int>(i.Version).Equals(new Tuple<int>(1))).ToQueryString()).Message);
    }
}
