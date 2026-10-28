using Microsoft.EntityFrameworkCore.Design;
using pengdows.efcore.adonet.Design;
using pengdows.efcore.adonet.mysql;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.mysql.MySqlDesignTimeServices")]

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     The provider's design-time services for MySQL: the core's, and the annotation code generator that writes MySQL's
///     annotations as their fluent methods.
/// </summary>
public class MySqlDesignTimeServices() : AdoNetDesignTimeServices(MySqlDialect.Instance)
{
    public override void ConfigureDesignTimeServices(Microsoft.Extensions.DependencyInjection.IServiceCollection serviceCollection)
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IAnnotationCodeGenerator, MySqlAnnotationCodeGenerator>(serviceCollection);
        base.ConfigureDesignTimeServices(serviceCollection);
    }
}
