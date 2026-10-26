using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Bulk;
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
    ///     <see cref="AdoNetOptionsExtension.ApplyServices" />. The dialect registers first, and everything here is TryAdd,
    ///     so the dialect's services win; anything neither registers uses EF's relational defaults.
    /// </summary>
    public static IServiceCollection AddEntityFrameworkAdoNet(this IServiceCollection services, IAdoNetDialect dialect)
    {
        dialect.ConfigureServices(services);

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
            .TryAdd<IMigrationsSqlGenerator, AdoNetMigrationsSqlGenerator>()
            .TryAdd<IRelationalAnnotationProvider, AdoNetAnnotationProvider>()
            .TryAdd<IMigrationsAnnotationProvider, AdoNetMigrationsAnnotationProvider>()
            .TryAdd<IQueryCompilationContextFactory, AdoNetQueryCompilationContextFactory>()
            .TryAdd<IQuerySqlGeneratorFactory, AdoNetQuerySqlGeneratorFactory>()
            .TryAdd<IMethodCallTranslatorProvider, AdoNetMethodCallTranslatorProvider>()
            .TryAdd<IMemberTranslatorProvider, AdoNetMemberTranslatorProvider>()
            .TryAdd<IRelationalSqlTranslatingExpressionVisitorFactory, AdoNetSqlTranslatingExpressionVisitorFactory>()
            .TryAdd<IEvaluatableExpressionFilter, AdoNetEvaluatableExpressionFilter>()
            .TryAdd<IQueryTranslationPostprocessorFactory, AdoNetQueryTranslationPostprocessorFactory>()
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, AdoNetQueryableMethodTranslatingExpressionVisitorFactory>()
            .TryAdd<IUpdateSqlGenerator, AdoNetUpdateSqlGenerator>()
            .TryAdd<IModificationCommandFactory, AdoNetModificationCommandFactory>()
            .TryAdd<IModificationCommandBatchFactory, AdoNetModificationCommandBatchFactory>()
            .TryAdd<IRelationalParameterBasedSqlProcessorFactory, AdoNetParameterBasedSqlProcessorFactory>()
            .TryAdd<IValueGeneratorSelector, AdoNetValueGeneratorSelector>()
            .TryAddProviderSpecificServices(b => b
                .TryAddSingleton(dialect)
                .TryAddSingleton(dialect.Sql)
                .TryAddSingleton(dialect.TypeMappings)
                .TryAddSingleton(dialect.Capabilities)
                .TryAddSingleton<AdoNetDataSources, AdoNetDataSources>()
                .TryAddScoped<IAdoNetBulkLoader, AdoNetInsertBulkLoader>())
            .TryAddCoreServices();

        return services;
    }
}
