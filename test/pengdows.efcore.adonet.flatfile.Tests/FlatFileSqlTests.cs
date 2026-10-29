using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

public class FlatFileSqlTests
{
    [Fact]
    public void Advertises_independent_active_readers()
        => Assert.True(FlatFileDialect.Instance.Capabilities.SupportsMultipleActiveReaders);

    [Fact]
    public void Uses_pengdows_delta_tables_with_include_for_store_generated_values()
    {
        var keys = Assert.IsType<DeltaTableKeyRetrieval>(FlatFileDialect.Instance.Sql.Keys);
        Assert.True(keys.UseIncludeColumns);
    }

    [Fact]
    public void Advertises_iso_json_queries_and_json_table_without_vendor_mutation()
    {
        var json = FlatFileDialect.Instance.Sql.Json!;
        Assert.Equal("JSON", json.StoreType);
        Assert.Contains("JSON_VALUE", json.Value);
        Assert.Contains("JSON_TABLE", json.ArrayElements);
        Assert.Null(json.Set);
    }

    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Uses_colon_parameters_for_values_and_paging()
    {
        using var context = Context(b => b.UseAdoNetFlatFile("path=/nonexistent"));

        var sql = PagedQuery(context);

        Assert.Contains("= :name", sql);
        Assert.Matches(@"OFFSET :p\w* ROWS FETCH NEXT :p\w* ROWS ONLY", sql);
    }

    [Fact]
    public void Uses_interval_parameters_for_time_of_day_arithmetic()
    {
        using var context = Context(b => b.UseAdoNetFlatFile("path=/nonexistent"));
        var sql = context.Blogs.Select(b => (b.CreatedAt.TimeOfDay + TimeSpan.FromHours(1)).Hours).ToQueryString();

        Assert.Contains("INTERVAL '01:00:00' HOUR TO SECOND", sql, StringComparison.OrdinalIgnoreCase);
    }
}
