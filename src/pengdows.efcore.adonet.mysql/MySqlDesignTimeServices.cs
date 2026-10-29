using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;
using pengdows.efcore.adonet.mysql;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.mysql.MySqlDesignTimeServices")]

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     The provider's design-time services for MySQL: the core's, the annotation code generator that writes MySQL's
///     annotations as their fluent methods, and reverse engineering that opens a connection string through MySqlConnector and
///     tells MySQL from MariaDB by the server.
/// </summary>
public class MySqlDesignTimeServices() : AdoNetDesignTimeServices(MySqlDialect.Instance, MySqlConnector.MySqlConnectorFactory.Instance)
{
    public override void ConfigureDesignTimeServices(Microsoft.Extensions.DependencyInjection.IServiceCollection serviceCollection)
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IAnnotationCodeGenerator, MySqlAnnotationCodeGenerator>(serviceCollection);
        base.ConfigureDesignTimeServices(serviceCollection);
    }

    protected override System.Reflection.MethodInfo UseProvider
        => AdoNetProviderCodeGenerator.UseMethod(typeof(MySqlDbContextOptionsBuilderExtensions), nameof(MySqlDbContextOptionsBuilderExtensions.UseAdoNetMySql));

    // The server is told from the connection, as a design-time context has no options to name it: MySQL, MariaDB, and
    // either's version, each read through its own catalog.
    protected override Microsoft.EntityFrameworkCore.Scaffolding.IDatabaseModelFactory? CreateDatabaseModelFactory()
        => new MySqlDatabaseModelFactory();
}

internal sealed class MySqlDatabaseModelFactory()
    : pengdows.efcore.adonet.Scaffolding.AdoNetDatabaseModelFactory(MySqlDialect.Instance, MySqlConnector.MySqlConnectorFactory.Instance)
{
    protected override IAdoNetDialect DialectFor(System.Data.Common.DbConnection connection)
        => MySqlDialect.FromServerVersion(connection);
}
