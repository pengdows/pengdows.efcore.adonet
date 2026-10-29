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
        : this(dialect, null)
    {
    }

    /// <summary>
    ///     For a plugin that names its driver: EF's tools scaffold from a connection string (dotnet ef dbcontext scaffold),
    ///     which is opened through <paramref name="driverFactory" />. Without one, a schema is read only from a connection.
    /// </summary>
    protected AdoNetDesignTimeServices(IAdoNetDialect dialect, System.Data.Common.DbProviderFactory? driverFactory)
    {
        Dialect = dialect;
        _driverFactory = driverFactory;
    }

    private readonly System.Data.Common.DbProviderFactory? _driverFactory;

    protected virtual IAdoNetDialect Dialect { get; }

    /// <summary>
    ///     The reverse engineering of a plugin that names its driver, or one that tells servers apart from the connection;
    ///     null for the core's own, which reads a schema only from a connection.
    /// </summary>
    protected virtual IDatabaseModelFactory? CreateDatabaseModelFactory()
        => _driverFactory is { } driverFactory ? new AdoNetDatabaseModelFactory(Dialect, driverFactory) : null;

    /// <summary>
    ///     The plugin's <c>UseAdoNet...</c> entry point, which scaffolding writes into <c>OnConfiguring</c> (see
    ///     <see cref="AdoNetProviderCodeGenerator.UseMethod" />); null for the core's own, which names no driver, so a model
    ///     is scaffolded but no <c>OnConfiguring</c> configuration can be.
    /// </summary>
    protected virtual System.Reflection.MethodInfo? UseProvider
        => null;

    public virtual void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        if (CreateDatabaseModelFactory() is { } modelFactory)
        {
            serviceCollection.AddSingleton(modelFactory);
        }

        if (UseProvider is { } useProvider)
        {
            serviceCollection.AddSingleton<IProviderConfigurationCodeGenerator>(
                p => new AdoNetProviderCodeGenerator(p.GetRequiredService<ProviderCodeGeneratorDependencies>(), useProvider));
        }

        serviceCollection.AddEntityFrameworkAdoNet(Dialect);
        new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection)
            .TryAdd<IDatabaseModelFactory, AdoNetDatabaseModelFactory>()
            .TryAddCoreServices();
    }
}
