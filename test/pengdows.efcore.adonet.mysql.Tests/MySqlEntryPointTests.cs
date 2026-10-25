using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MySqlConnector;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>Every entry point configures the MySQL dialect, with core's argument order.</summary>
public class MySqlEntryPointTests
{
    private const string ConnectionString = "Server=localhost";

    private static void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(MySqlDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql(ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = new MySqlConnection("Server=localhost");
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql(connection, contextOwnsConnection: false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = MySqlConnectorFactory.Instance.CreateDataSource(ConnectionString);

        UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql(dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql().Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(MySqlDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNetMySql(ConnectionString);

        UsesTheDialect(builder.Options);
    }
}
