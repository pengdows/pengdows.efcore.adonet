using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

public class SqlServerSqlTests
{
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
    public void Uses_top_without_an_offset()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var sql = context.Blogs.Take(3).ToQueryString();

        Assert.Matches(@"SELECT TOP\(@p\w*\) ", sql);
        Assert.DoesNotContain("FETCH", sql);
    }

    [Fact]
    public void Orders_by_a_constant_when_offset_has_no_ordering()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Matches(@"ORDER BY \(SELECT 1\)\s+OFFSET @p\w* ROWS", context.Blogs.Skip(3).ToQueryString());
        Assert.Matches(@"OFFSET @p\w* ROWS FETCH NEXT @p\w* ROWS ONLY", PagedQuery(context));
    }

    [Fact]
    public void Converts_between_bit_values_and_conditions()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        var where = context.Blogs.Where(b => b.IsActive && !b.IsActive).ToQueryString();
        var select = context.Blogs.Select(b => new { Big = b.Rating > 5, b.IsActive }).ToQueryString();

        Assert.Contains("WHERE \"b\".\"IsActive\" = CAST(1 AS BIT) AND NOT (\"b\".\"IsActive\" = CAST(1 AS BIT))", where);
        Assert.Contains("CASE\n    WHEN \"b\".\"Rating\" > 5.0 THEN CAST(1 AS BIT)\n    ELSE CAST(0 AS BIT)\nEND AS \"Big\"", select.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Long_count_uses_count_big()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.Contains("COUNT_BIG(*)", context.Blogs.GroupBy(b => b.Name).Select(g => g.LongCount()).ToQueryString());
        Assert.DoesNotContain("COUNT_BIG", context.Blogs.GroupBy(b => b.Name).Select(g => g.Count()).ToQueryString());
    }

    [Fact]
    public void Bulk_loads_use_the_native_loader()
    {
        using var context = Context(b => b.UseAdoNetSqlServer("Server=localhost"));

        Assert.IsType<SqlServerBulkLoader>(context.GetService<IAdoNetBulkLoader>());
    }
}
