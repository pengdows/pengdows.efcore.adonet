using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

public class MySqlSqlTests
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
    public void Uses_backticks_and_a_limit_for_offset_only()
    {
        using var context = Context(b => b.UseAdoNetMySql("Server=localhost"));

        var sql = context.Blogs.OrderBy(b => b.Id).Skip(5).ToQueryString();

        Assert.Contains("FROM `Blogs` AS `b`", sql);
        Assert.Matches(@"LIMIT 18446744073709551615\s+OFFSET @p", sql);
    }
}
