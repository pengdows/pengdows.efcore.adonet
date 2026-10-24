using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

public class FlatFileSqlTests
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
    public void Uses_colon_parameters_and_literal_paging()
    {
        using var context = Context(b => b.UseAdoNetFlatFile("path=/nonexistent"));

        var sql = PagedQuery(context);

        Assert.Contains("= :name", sql);
        Assert.Contains("OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY", sql);
    }
}
