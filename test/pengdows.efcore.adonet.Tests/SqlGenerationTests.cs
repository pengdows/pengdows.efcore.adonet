using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Core SQL and configuration behavior, with the ANSI dialect, checked without touching a database.</summary>
public class SqlGenerationTests
{
    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>().AllowManyDialects();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    [Fact]
    public void Ansi_uses_offset_fetch_and_at_parameters()
    {
        using var context = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:", o => o.UseDialect(AdoNetDialect.Ansi)));
        var name = "x";

        var sql = context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();

        Assert.Contains("WHERE \"b\".\"Name\" = @name", sql);
        Assert.Matches(@"OFFSET @p\w* ROWS FETCH NEXT @p\w* ROWS ONLY", sql);
    }

    [Fact]
    public void Same_dialect_instance_reuses_the_internal_service_provider()
    {
        // Singleton services are shared exactly when the internal service provider is.
        using var first = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=one;Mode=Memory"));
        using var second = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=two;Mode=Memory"));

        Assert.Same(first.GetService<IQuerySqlGeneratorFactory>(), second.GetService<IQuerySqlGeneratorFactory>());
    }

    [Fact]
    public void Configuring_two_connection_sources_is_rejected()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = Context(b => b
            .UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:")
            .UseAdoNet(connection));

        Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection());
    }
}
