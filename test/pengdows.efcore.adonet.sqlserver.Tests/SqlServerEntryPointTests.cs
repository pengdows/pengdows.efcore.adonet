using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.SqlClient;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>Every entry point configures the SQL Server dialect, with core's argument order.</summary>
public class SqlServerEntryPointTests
{
    private const string ConnectionString = "Server=localhost";

    private static void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(SqlServerDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer(ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = new SqlConnection("Server=localhost");
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer(connection, contextOwnsConnection: false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = SqlClientFactory.Instance.CreateDataSource(ConnectionString);

        UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer(dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer().Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(SqlServerDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNetSqlServer(ConnectionString);

        UsesTheDialect(builder.Options);
    }
}
