using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.Tests;

/// <summary>How a factory and connection string become connections.</summary>
public class ConnectionSourceTests
{
    private static BlogContext Context(DbProviderFactory factory, string connectionString)
        => new(new DbContextOptionsBuilder<BlogContext>().UseAdoNet(factory, connectionString).AllowManyDialects().Options);

    [Fact]
    public void A_factory_is_used_through_one_data_source_per_connection_string()
    {
        var factory = new CountingFactory();

        using (var first = Context(factory, "Data Source=a;Mode=Memory"))
        using (var second = Context(factory, "Data Source=a;Mode=Memory"))
        using (var other = Context(factory, "Data Source=b;Mode=Memory"))
        {
            first.Database.GetDbConnection();
            second.Database.GetDbConnection();
            other.Database.GetDbConnection();
        }

        Assert.Equal(["Data Source=a;Mode=Memory", "Data Source=b;Mode=Memory"], factory.DataSourcesCreated);
    }

    [Fact]
    public void Set_connection_string_is_honored()
    {
        var factory = new CountingFactory();
        using var context = Context(factory, "Data Source=a;Mode=Memory");

        context.Database.SetConnectionString("Data Source=c;Mode=Memory");

        Assert.Equal("Data Source=c;Mode=Memory", context.Database.GetDbConnection().ConnectionString);
        Assert.Equal(["Data Source=c;Mode=Memory"], factory.DataSourcesCreated);
    }

    /// <summary>A provider that, like Npgsql and MySqlConnector, would pool per data source.</summary>
    private sealed class CountingFactory : DbProviderFactory
    {
        public List<string> DataSourcesCreated { get; } = [];

        public override DbConnection CreateConnection()
            => SqliteFactory.Instance.CreateConnection();

        public override DbDataSource CreateDataSource(string connectionString)
        {
            lock (DataSourcesCreated)
            {
                DataSourcesCreated.Add(connectionString);
            }

            return base.CreateDataSource(connectionString);
        }
    }
}
