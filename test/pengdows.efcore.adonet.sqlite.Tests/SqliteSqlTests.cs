using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>SQLite's SQL shape, checked without touching a database.</summary>
public class SqliteSqlTests
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
        using var context = Context(b => b.UseAdoNetSqlite("Data Source=:memory:"));

        Assert.Matches(@"LIMIT @p\w*\s+OFFSET @p\w*", PagedQuery(context));
    }

    [Fact]
    public void Offset_only_emits_no_limit_value()
    {
        using var context = Context(b => b.UseAdoNetSqlite("Data Source=:memory:"));

        Assert.Contains("LIMIT -1", context.Blogs.OrderBy(b => b.Id).Skip(5).ToQueryString());
    }

    [Fact]
    public void Bool_constants_use_dialect_literals()
    {
        using var context = Context(b => b.UseAdoNetSqlite("Data Source=:memory:"));

        Assert.Contains("TRUE AS \"Flag\"", context.Blogs.Select(b => new { b.Id, Flag = true }).ToQueryString());
    }

    [Fact]
    public void Schemas_are_dropped()
    {
        using var context = Context(b => b.UseAdoNetSqlite("Data Source=:memory:"));

        Assert.Equal("\"T\"", context.GetService<ISqlGenerationHelper>().DelimitIdentifier("T", "dbo"));
    }

    [Fact]
    public void Registered_dialect_is_found_from_the_factory_connection_and_data_source()
    {
        SqliteDbContextOptionsBuilderExtensions.RegisterDialect();
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var dataSource = SqliteFactory.Instance.CreateDataSource("Data Source=:memory:");

        using var fromFactory = Context(b => b.UseAdoNet(SqliteFactory.Instance, "Data Source=:memory:"));
        using var fromConnection = Context(b => b.UseAdoNet(connection));
        using var fromDataSource = Context(b => b.UseAdoNet(dataSource));

        Assert.All([fromFactory, fromConnection, fromDataSource], c => Assert.Same(SqliteDialect.Instance, c.GetService<IAdoNetDialect>()));
    }
}
