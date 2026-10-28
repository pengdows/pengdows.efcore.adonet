using System.Data.Common;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using Microsoft.EntityFrameworkCore;

using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>How a factory and connection string become connections.</summary>
public class ConnectionSourceTests
{
    private static BlogContext Context(DbProviderFactory factory, string connectionString)
        => new(new DbContextOptionsBuilder<BlogContext>().UseAdoNet(factory, connectionString, AdoNetDialect.Ansi).AllowManyDialects().Options);

    [Fact]
    public void A_factory_is_used_through_one_data_source_per_connection_string()
    {
        var factory = PoolingFactory();

        using (var first = Context(factory, "Data Source=a;Mode=Memory"))
        using (var second = Context(factory, "Data Source=a;Mode=Memory"))
        using (var other = Context(factory, "Data Source=b;Mode=Memory"))
        {
            first.Database.GetDbConnection();
            second.Database.GetDbConnection();
            other.Database.GetDbConnection();
        }

        Assert.Equal(["Data Source=a;Mode=Memory", "Data Source=b;Mode=Memory"], DataSourcesCreated(factory));
    }

    [Fact]
    public void Set_connection_string_is_honored()
    {
        var factory = PoolingFactory();
        using var context = Context(factory, "Data Source=a;Mode=Memory");

        context.Database.SetConnectionString("Data Source=c;Mode=Memory");

        Assert.Equal("Data Source=c;Mode=Memory", context.Database.GetDbConnection().ConnectionString);
        Assert.Equal(["Data Source=c;Mode=Memory"], DataSourcesCreated(factory));
    }

    /// <summary>A provider that, like Npgsql and MySqlConnector, pools per data source.</summary>
    private static fakeDbFactory PoolingFactory()
        => new(SupportedDatabase.PostgreSql) { SupportsNativeDataSource = true };

    private static IEnumerable<string> DataSourcesCreated(fakeDbFactory factory)
        => factory.CreatedDataSources.Select(d => d.ConnectionString);
}
