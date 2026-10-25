using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>Every entry point configures the PostgreSQL dialect, with core's argument order.</summary>
public class PostgreSqlEntryPointTests
{
    private const string ConnectionString = "Host=localhost";

    private static void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetPostgreSql(ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = new NpgsqlConnection("Host=localhost");
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetPostgreSql(connection, contextOwnsConnection: false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = NpgsqlFactory.Instance.CreateDataSource(ConnectionString);

        UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetPostgreSql(dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetPostgreSql().Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNetPostgreSql(ConnectionString);

        UsesTheDialect(builder.Options);
    }
}
