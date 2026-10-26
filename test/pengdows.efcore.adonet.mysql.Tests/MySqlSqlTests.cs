using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

/// <summary>
///     SingleStore speaks MySQL but accepts SAVEPOINT and ROLLBACK TO SAVEPOINT and ignores them, so rolled-back work is
///     committed. MySqlConnector reports savepoints there anyway (it declined to tell servers apart:
///     https://github.com/mysql-net/MySqlConnector/issues/1677), so the dialect says there are none.
/// </summary>
public class SingleStoreSavepointTests
{
    [Fact]
    public async Task SingleStore_has_no_savepoints_whatever_the_driver_reports()
    {
        await using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(MySqlDialect.SingleStore))
            .Options);

        Assert.Equal("SingleStore", MySqlDialect.SingleStore.Name);
        Assert.Null(MySqlDialect.SingleStore.Sql.Savepoints);
        Assert.NotNull(MySqlDialect.Instance.Sql.Savepoints);
        Assert.Same(MySqlDialect.SingleStore, context.GetService<IAdoNetDialect>());
    }
}
