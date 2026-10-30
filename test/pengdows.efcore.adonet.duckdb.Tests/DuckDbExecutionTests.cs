using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Testing;
using static pengdows.efcore.adonet.duckdb.Tests.DuckDbJsonTests;
using static pengdows.efcore.adonet.duckdb.Tests.DuckDbSequenceTests;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     The DuckDB features of <see cref="DuckDbJsonTests" />, <see cref="DuckDbSequenceTests" /> and <see cref="DuckDbSqlTests" />
///     run through fakeDb emulating DuckDB: the statements and parameters sent, scripted rows read back as .NET values, and
///     the DDL executed. What DuckDB computes is the integration suites'.
/// </summary>
public class DuckDbExecutionTests
{
    private readonly FakeDuckDb _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_paged_query_sends_dollar_parameters_and_reads_its_rows()
    {
        await using var context = _db.Context<BlogContext>(o => new BlogContext(o));
        var name = "x";
        _db.Returns([3]);

        var ids = await context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).Select(b => b.Id).ToListAsync(Ct);

        Assert.Equal([3], ids);
        Assert.Matches(@"LIMIT \$p\w*\s+OFFSET \$p\w*", Assert.Single(_db.Commands));
        Assert.Contains("name='x'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task Json_columns_read_back_as_the_collection_and_the_owned_type()
    {
        await using var context = _db.Context<GadgetContext>(o => new GadgetContext(o));
        _db.Returns([1, "[1,2,3]", "{\"Color\":\"red\",\"Weight\":4}"]);

        var gadget = await context.Gadgets.SingleAsync(Ct);

        Assert.Equal([1, 2, 3], gadget.Tags);
        Assert.Equal(("red", 4), (gadget.Spec!.Color, gadget.Spec.Weight));
    }

    [Fact]
    public async Task A_json_property_comparison_sends_its_value_as_a_parameter()
    {
        await using var context = _db.Context<GadgetContext>(o => new GadgetContext(o));
        var color = "red";
        _db.Returns([1]);

        var ids = await context.Gadgets.Where(g => g.Spec!.Color == color).Select(g => g.Id).ToListAsync(Ct);

        Assert.Equal([1], ids);
        Assert.Contains("json_extract_string(\"g\".\"Spec\", '$.Color') = $color", Assert.Single(_db.Commands));
        Assert.Contains("color='red'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task A_collection_contains_is_sent_over_json_each_with_its_value_as_a_parameter()
    {
        await using var context = _db.Context<GadgetContext>(o => new GadgetContext(o));
        var tag = 3;
        _db.Returns([1], [2]);

        var ids = await context.Gadgets.Where(g => g.Tags.Contains(tag)).Select(g => g.Id).ToListAsync(Ct);

        Assert.Equal([1, 2], ids);
        Assert.Contains("json_each(\"g\".\"Tags\")", Assert.Single(_db.Commands));
        Assert.Contains("tag='3'", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task A_collection_is_written_as_json_text()
    {
        await using var context = _db.Context<GadgetContext>(o => new GadgetContext(o));
        _db.Returns([8]);
        var gadget = new Gadget { Tags = [5, 6], Spec = new Spec { Color = "blue", Weight = 2 } };

        context.Add(gadget);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(8, gadget.Id);
        Assert.Contains("RETURNING \"Id\"", Assert.Single(_db.Commands));
        Assert.Contains("'[5,6]'", Assert.Single(_db.Parameters));
        Assert.Contains("\"Color\":\"blue\"", Assert.Single(_db.Parameters));
    }

    [Fact]
    public async Task A_sequence_key_is_read_back_from_the_insert()
    {
        await using var context = _db.Context<ShopContext>(o => new ShopContext(o));
        _db.Returns([41, 7L]);
        var order = new Order();

        context.Add(order);
        await context.SaveChangesAsync(Ct);

        Assert.Equal((41, 7L), (order.Id, order.Number));
        Assert.Contains("RETURNING \"Id\", \"Number\"", Assert.Single(_db.Commands));
    }

    [Fact]
    public void The_sequence_ddl_is_executed_before_its_table()
    {
        using var context = _db.Context<ShopContext>(o => new ShopContext(o));

        context.GetService<IRelationalDatabaseCreator>().CreateTables();

        Assert.Equal("CREATE SEQUENCE \"Orders_Id_seq\";", _db.Commands[0]);
        Assert.Equal("CREATE SEQUENCE \"Orders_Number_seq\";", _db.Commands[1]);
        Assert.StartsWith("CREATE TABLE \"Orders\"", _db.Commands[2]);
    }

    [Fact]
    public async Task EF_Functions_Random_is_DuckDBs_random()
    {
        await using var context = _db.Context<BlogContext>(o => new BlogContext(o));
        _db.Returns([0.25]);

        var values = await context.Blogs.Select(_ => EF.Functions.Random()).ToListAsync(Ct);

        Assert.Equal([0.25], values);
        Assert.Contains("random()", Assert.Single(_db.Commands));
        Assert.DoesNotContain("RAND", Assert.Single(_db.Commands));
    }
}
