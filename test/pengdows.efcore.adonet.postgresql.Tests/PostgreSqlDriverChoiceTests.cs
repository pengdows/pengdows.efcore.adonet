using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Npgsql's own entry points (Npgsql.EntityFrameworkCore.PostgreSQL's UseNpgsql, on DbContextOptionsBuilder and its generic),
///     which every EF PostgreSQL provider exposes: the same four forms as UseAdoNetPostgreSql, and the same dialect.
/// </summary>

/// <summary>
///     Choosing the ADO.NET driver at the provider's own entry point: the factory's connections are the ones the context uses,
///     and the PostgreSQL dialect is the same whichever driver it is. (MySQL swaps MySqlConnector and MySql.Data this way.)
/// </summary>
public class PostgreSqlDriverChoiceTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql";

    [Fact]
    public void A_factory_supplies_the_connections_and_the_postgresql_dialect_stays()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
            .UseAdoNetPostgreSql(factory, ConnectionString)
            .Options);

        Assert.Same(PostgreSqlDialect.Instance, context.GetService<pengdows.efcore.adonet.IAdoNetDialect>());
        Assert.IsType<fakeDbConnection>(context.Database.GetDbConnection());
        Assert.Single(factory.CreatedConnections);
    }

    [Fact]
    public void The_default_driver_is_unchanged_without_a_factory()
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
            .UseAdoNetPostgreSql("Host=localhost")
            .Options);

        Assert.IsType<Npgsql.NpgsqlConnection>(context.Database.GetDbConnection());
    }
}
