using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     A primitive collection stored as DuckDB's own LIST (<c>UseList()</c>, or a <c>T[]</c> column type) rather than JSON, as
///     the DuckDB EF provider stores one: its Contains, Count and element at a position are list_contains, len and a 1-based
///     index, and any other query reads the rows of unnest with generate_subscripts. A collection not made a list stays JSON.
///     SQL shape, and run through fakeDb emulating DuckDB. Behaviour reference (not copied):
///     https://github.com/skuirrels/DuckDB.EFCoreProvider/tree/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Query
/// </summary>
public class DuckDbListTests
{
    private readonly FakeDuckDb _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private static BoxContext Context()
        => new(new DbContextOptionsBuilder<BoxContext>().UseAdoNetDuckDb("Data Source=:memory:").Options);

    private static string Sql<T>(Func<IQueryable<Box>, IQueryable<T>> query)
    {
        using var context = Context();
        return query(context.Boxes).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void A_list_column_is_the_elements_list_type_and_a_json_one_stays_json()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Numbers\" INTEGER[] NOT NULL", script);
        Assert.Contains("\"Names\" VARCHAR[] NOT NULL", script);
        Assert.Contains("\"Tags\" JSON NOT NULL", script);
    }

    [Fact]
    public void Contains_count_and_indexing_are_list_functions()
    {
        Assert.Contains("list_contains(\"b\".\"Numbers\", 3)", Sql(s => s.Where(b => b.Numbers.Contains(3)).Select(b => b.Id)));
        Assert.Contains("len(\"b\".\"Names\")", Sql(s => s.Select(b => b.Names.Count)));
        Assert.Contains("\"b\".\"Numbers\"[1 + 1]", Sql(s => s.Select(b => b.Numbers[1])));
        Assert.Contains("json_each(\"b\".\"Tags\")", Sql(s => s.Where(b => b.Tags.Contains(3)).Select(b => b.Id)));
    }

    [Fact]
    public void Any_other_query_reads_the_rows_of_unnest()
        => Assert.Matches(
            "FROM \\(SELECT generate_subscripts\\(\"b\"\\.\"Numbers\", 1\\) AS \"key\", unnest\\(\"b\"\\.\"Numbers\"\\) AS \"value\"\\) AS \"\\w+\"",
            Sql(s => s.Select(b => b.Numbers.Where(n => n > 2).Sum())));

    [Fact]
    public void A_list_constant_is_a_list_literal_of_its_type()
    {
        using var context = Context();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(List<string>), "VARCHAR[]")!;

        Assert.Equal("['a''b', 'c']::VARCHAR[]", mapping.GenerateSqlLiteral(new List<string> { "a'b", "c" }));
    }

    [Fact]
    public async Task A_list_is_sent_and_read_back_as_a_list()
    {
        await using var context = _db.Context<BoxContext>(o => new BoxContext(o));
        _db.Returns([4]);
        var box = new Box { Numbers = [1, 2], Names = ["x"], Tags = [] };

        context.Add(box);
        await context.SaveChangesAsync(Ct);
        _db.Returns([4, new List<string> { "x", "y" }, new List<int> { 1, 2 }, "[]"]);
        var read = await context.Boxes.AsNoTracking().SingleAsync(Ct);

        Assert.Equal(4, box.Id);
        Assert.Equal([1, 2], read.Numbers);
        Assert.Equal(["x", "y"], read.Names);
        Assert.Contains("INSERT INTO \"Boxes\"", _db.Commands[0]);
    }

    [Fact]
    public async Task List_contains_sends_the_element_as_a_parameter()
    {
        await using var context = _db.Context<BoxContext>(o => new BoxContext(o));
        var three = 3;
        _db.Returns([4]);

        Assert.Equal([4], await context.Boxes.Where(b => b.Numbers.Contains(three)).Select(b => b.Id).ToListAsync(Ct));
        Assert.Contains("list_contains(\"b\".\"Numbers\", $three)", Assert.Single(_db.Commands));
        Assert.Contains("three='3'", Assert.Single(_db.Parameters));
    }

    public class Box
    {
        public int Id { get; set; }
        public List<int> Numbers { get; set; } = [];
        public List<string> Names { get; set; } = [];
        public List<int> Tags { get; set; } = [];
    }

    public sealed class BoxContext(DbContextOptions<BoxContext> options) : DbContext(options)
    {
        public DbSet<Box> Boxes => Set<Box>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Box>().Property(b => b.Numbers).UseList();
            modelBuilder.Entity<Box>().Property(b => b.Names).HasColumnType("VARCHAR[]");
        }
    }
}
