using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     EF's tools scaffold from a connection string (dotnet ef dbcontext scaffold): the MySQL design-time services name
///     MySqlConnector as the driver, so the string is opened through it and the server's schema read.
/// </summary>
public abstract class MySqlScaffoldingTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : MySqlFixture
{
    [Fact]
    public void A_connection_string_is_scaffolded_through_the_mysql_driver()
    {
        var services = new ServiceCollection();
        new MySqlDesignTimeServices().ConfigureDesignTimeServices(services);
        var factory = services.BuildServiceProvider().GetRequiredService<IDatabaseModelFactory>();

        var model = factory.Create(fixture.ConnectionString, new DatabaseModelFactoryOptions(["Blogs", "Posts"]));

        Assert.Equal(["Blogs", "Posts"], model.Tables.Select(t => t.Name).Order());
        Assert.Contains("Name", model.Tables.Single(t => t.Name == "Blogs").Columns.Select(c => c.Name));
    }
}

public class MySqlConnectorScaffoldingTests(MySqlFixture fixture) : MySqlScaffoldingTests<MySqlFixture>(fixture);

public class MariaDbScaffoldingTests(MariaDbFixture fixture) : MySqlScaffoldingTests<MariaDbFixture>(fixture);
