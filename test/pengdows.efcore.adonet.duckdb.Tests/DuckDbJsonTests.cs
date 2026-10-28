using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     DuckDB's JSON type and its json extension's functions: a JSON column (an owned or complex type <c>ToJson()</c>, or a
///     primitive collection) is read with json_extract_string and json_extract, and a primitive collection is queried as the
///     rows of json_each, as the DuckDB EF provider queries a JSON collection. SQL shape only. Behaviour reference (not copied):
///     https://github.com/skuirrels/DuckDB.EFCoreProvider/tree/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Query
/// </summary>
public class DuckDbJsonTests
{
    private static GadgetContext Context()
        => new(new DbContextOptionsBuilder<GadgetContext>().UseAdoNetDuckDb("Data Source=:memory:").Options);

    private static string Sql<T>(Func<IQueryable<Gadget>, IQueryable<T>> query)
    {
        using var context = Context();
        return query(context.Gadgets).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void Json_columns_are_the_json_type()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Tags\" JSON NOT NULL", script);
        Assert.Contains("\"Spec\" JSON", script);
    }

    [Fact]
    public void A_json_property_is_read_with_json_extract_string()
        => Assert.Contains(
            "json_extract_string(\"g\".\"Spec\", '$.Color')",
            Sql(s => s.Where(g => g.Spec!.Color == "red").Select(g => g.Id)));

    [Fact]
    public void A_typed_json_property_is_cast_from_its_text()
        => Assert.Contains(
            "CAST(json_extract_string(\"g\".\"Spec\", '$.Weight') AS INTEGER)",
            Sql(s => s.Where(g => g.Spec!.Weight > 3).Select(g => g.Id)));

    [Fact]
    public void A_primitive_collection_is_queried_as_the_rows_of_json_each()
    {
        var sql = Sql(s => s.Where(g => g.Tags.Contains(3)).Select(g => g.Id));

        Assert.Contains("json_each(\"g\".\"Tags\")", sql);
        Assert.Contains("CAST(json_extract_string(\"value\", '$') AS INTEGER)", sql);
    }

    [Fact]
    public void A_primitive_collection_element_is_read_by_its_position()
        => Assert.Contains("json_each(\"g\".\"Tags\")", Sql(s => s.Where(g => g.Tags.Count > 1).Select(g => g.Tags[0])));

    public class Gadget
    {
        public int Id { get; set; }
        public List<int> Tags { get; set; } = [];
        public Spec? Spec { get; set; }
    }

    public class Spec
    {
        public string? Color { get; set; }
        public int Weight { get; set; }
    }

    public sealed class GadgetContext(DbContextOptions<GadgetContext> options) : DbContext(options)
    {
        public DbSet<Gadget> Gadgets => Set<Gadget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gadget>().OwnsOne(g => g.Spec, b => b.ToJson());
    }
}
