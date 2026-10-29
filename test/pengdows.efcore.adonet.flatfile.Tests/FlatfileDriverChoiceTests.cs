using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.flatfile;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

/// <summary>
///     Choosing the ADO.NET driver at this provider's entry point: a factory's connections are the ones the context uses, and
///     the dialect stays FlatFileDialect. Driver swapping, as MySqlConnector and MySql.Data for MySQL.
/// </summary>
public class FlatfileDriverChoiceTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=FlatFile";

    [Fact]
    public void A_factory_supplies_the_connections_and_the_dialect_stays()
    {
        var factory = new fakeDbFactory(SupportedDatabase.FlatFile);
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .UseAdoNetFlatFile(factory, ConnectionString)
            .Options);

        Assert.Same(FlatFileDialect.Instance, context.GetService<IAdoNetDialect>());
        Assert.IsType<fakeDbConnection>(context.Database.GetDbConnection());
        Assert.Single(factory.CreatedConnections);
    }
}
