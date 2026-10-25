using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.flatfile;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

/// <summary>Every entry point configures the pengdows.flatfile dialect, with core's argument order.</summary>
public class FlatFileEntryPointTests
{
    private const string ConnectionString = "path=.";

    private static void UsesTheDialect(DbContextOptions<BlogContext> options)
    {
        using var context = new BlogContext(options);
        Assert.Same(FlatFileDialect.Instance, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void A_connection_string()
        => UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetFlatFile(ConnectionString).Options);

    [Fact]
    public void A_connection_the_context_may_own()
    {
        using var connection = new FlatFileConnection { ConnectionString = "path=." };
        var options = new DbContextOptionsBuilder<BlogContext>().UseAdoNetFlatFile(connection, contextOwnsConnection: false, o => o.CommandTimeout(5)).Options;

        UsesTheDialect(options);
        using var context = new BlogContext(options);
        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Equal(5, context.Database.GetCommandTimeout());
    }

    [Fact]
    public void A_data_source()
    {
        using var dataSource = FlatFileProviderFactory.Instance.CreateDataSource(ConnectionString);

        UsesTheDialect(new DbContextOptionsBuilder<BlogContext>().UseAdoNetFlatFile(dataSource).Options);
    }

    [Fact]
    public void A_connection_string_set_later()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetFlatFile().Options);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Same(FlatFileDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.Equal(ConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void The_non_generic_builder_works_too()
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        ((DbContextOptionsBuilder)builder).UseAdoNetFlatFile(ConnectionString);

        UsesTheDialect(builder.Options);
    }
}
