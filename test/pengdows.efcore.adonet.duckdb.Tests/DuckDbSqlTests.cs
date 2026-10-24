using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

public class DuckDbSqlTests
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
    public void Uses_dollar_parameters_and_limit_offset()
    {
        using var context = Context(b => b.UseAdoNetDuckDb("Data Source=:memory:"));

        var sql = PagedQuery(context);

        Assert.Contains("= $name", sql);
        Assert.Matches(@"LIMIT \$p\w*\s+OFFSET \$p\w*", sql);
    }
}
