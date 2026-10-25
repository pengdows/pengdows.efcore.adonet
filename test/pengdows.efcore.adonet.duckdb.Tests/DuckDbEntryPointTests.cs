using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DuckDB.NET.Data;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>Every entry point configures the DuckDB dialect, with core's argument order.</summary>
public class DuckDbEntryPointTests
{
    private const string ConnectionString = "Data Source=:memory:";

    private static void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(DuckDbDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb(ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb(connection, contextOwnsConnection: false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = DuckDBClientFactory.Instance.CreateDataSource(ConnectionString);

        UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb(dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetDuckDb().Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(DuckDbDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNetDuckDb(ConnectionString);

        UsesTheDialect(builder.Options);
    }
}
