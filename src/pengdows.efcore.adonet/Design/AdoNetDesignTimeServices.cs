using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Design;
using pengdows.efcore.adonet.Scaffolding;

[assembly: DesignTimeProviderServices("pengdows.efcore.adonet.Design.AdoNetDesignTimeServices")]

namespace pengdows.efcore.adonet.Design;

/// <summary>
///     The provider's design-time services (migrations code generation, reverse engineering), found by EF's tools through the
///     <see cref="DesignTimeProviderServicesAttribute" /> of the assembly the provider is named for: the dialect's. They hold
///     the provider's own services for the dialect, as EF's tools build them apart from any context's, and read the schema
///     through its <see cref="IAdoNetDialect.SchemaReader" />. A plugin derives one for its dialect.
/// </summary>
public class AdoNetDesignTimeServices : IDesignTimeServices
{
    public AdoNetDesignTimeServices()
        : this(AdoNetDialect.Ansi)
    {
    }

    protected AdoNetDesignTimeServices(IAdoNetDialect dialect)
        => Dialect = dialect;

    protected IAdoNetDialect Dialect { get; }

    public virtual void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddEntityFrameworkAdoNet(Dialect);
        new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection)
            .TryAdd<IDatabaseModelFactory, AdoNetDatabaseModelFactory>()
            .TryAddCoreServices();
    }
}
