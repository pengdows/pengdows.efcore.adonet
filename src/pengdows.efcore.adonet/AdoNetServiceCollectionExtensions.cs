using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Diagnostics;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.Metadata;
using pengdows.efcore.adonet.Migrations;
using pengdows.efcore.adonet.Query;
using pengdows.efcore.adonet.Storage;
using pengdows.efcore.adonet.Update;

namespace pengdows.efcore.adonet;

public static class AdoNetServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the provider's services in EF's internal service provider. Called by
    ///     <see cref="AdoNetOptionsExtension.ApplyServices" />; everything not listed here uses EF's relational defaults.
    /// </summary>
    public static IServiceCollection AddEntityFrameworkAdoNet(this IServiceCollection services, AdoNetSqlDialect dialect)
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<LoggingDefinitions, AdoNetLoggingDefinitions>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<AdoNetOptionsExtension>>()
            .TryAdd<IRelationalTypeMappingSource, AdoNetTypeMappingSource>()
            .TryAdd<ISqlGenerationHelper, AdoNetSqlGenerationHelper>()
            .TryAdd<IProviderConventionSetBuilder, AdoNetConventionSetBuilder>()
            .TryAdd<IRelationalConnection, AdoNetRelationalConnection>()
            .TryAdd<IRelationalTransactionFactory, AdoNetRelationalTransactionFactory>()
            .TryAdd<IRelationalDatabaseCreator, AdoNetDatabaseCreator>()
            .TryAdd<IHistoryRepository, AdoNetHistoryRepository>()
            .TryAdd<IQuerySqlGeneratorFactory, AdoNetQuerySqlGeneratorFactory>()
            .TryAdd<IUpdateSqlGenerator, AdoNetUpdateSqlGenerator>()
            .TryAdd<IModificationCommandBatchFactory, AdoNetModificationCommandBatchFactory>()
            .TryAdd<IRelationalParameterBasedSqlProcessorFactory, AdoNetParameterBasedSqlProcessorFactory>()
            .TryAdd<IValueGeneratorSelector, AdoNetValueGeneratorSelector>()
            .TryAddProviderSpecificServices(b => b.TryAddSingleton(dialect))
            .TryAddCoreServices();

        return services;
    }
}
