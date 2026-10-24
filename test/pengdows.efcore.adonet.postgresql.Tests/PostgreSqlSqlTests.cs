using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

public class PostgreSqlSqlTests
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
    public void Uses_limit_offset()
    {
        using var context = Context(b => b.UseAdoNetPostgreSql("Host=localhost"));

        var sql = PagedQuery(context);

        Assert.Contains("= @name", sql);
        Assert.Matches(@"LIMIT @p\w*\s+OFFSET @p\w*", sql);
    }

    [Fact]
    public void Bulk_loads_use_the_native_loader()
    {
        using var context = Context(b => b.UseAdoNetPostgreSql("Host=localhost"));

        Assert.IsType<PostgreSqlBulkLoader>(context.GetService<IAdoNetBulkLoader>());
    }
}
