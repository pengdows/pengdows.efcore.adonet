// Behaviour reference (not copied): EF Core's SQLite NetTopologySuite plugin (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.NTS/Infrastructure/SqliteNetTopologySuiteOptionsExtension.cs
// Copyright (c) .NET Foundation. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetTopologySuite;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>NetTopologySuite's spatial types, on a dialect with <see cref="ISqlDialect.Spatial" />.</summary>
public static class AdoNetNetTopologySuiteExtensions
{
    /// <summary>Maps NetTopologySuite's geometry types and translates their members.</summary>
    [RequiresDynamicCode("NetTopologySuite's types aren't supported with NativeAOT.")]
    public static AdoNetDbContextOptionsBuilder UseNetTopologySuite(this AdoNetDbContextOptionsBuilder optionsBuilder)
    {
        var builder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            builder.Options.FindExtension<AdoNetNetTopologySuiteOptionsExtension>() ?? new AdoNetNetTopologySuiteOptionsExtension());
        return optionsBuilder;
    }

    /// <summary>The spatial services, for an application that builds EF's internal service provider itself.</summary>
    [RequiresDynamicCode("NetTopologySuite's types aren't supported with NativeAOT.")]
    public static IServiceCollection AddEntityFrameworkAdoNetNetTopologySuite(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton(NtsGeometryServices.Instance);
        new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<IRelationalTypeMappingSourcePlugin, AdoNetSpatialTypeMappingSourcePlugin>()
            .TryAdd<IMethodCallTranslatorPlugin, AdoNetSpatialMethodCallTranslatorPlugin>()
            .TryAdd<IMemberTranslatorPlugin, AdoNetSpatialMemberTranslatorPlugin>();
        return serviceCollection;
    }
}

/// <summary>Adds the spatial services to the context's.</summary>
public class AdoNetNetTopologySuiteOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public virtual DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "UseNetTopologySuite carries the requirement.")]
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkAdoNetNetTopologySuite();

    public virtual void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => "using NetTopologySuite ";

        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["NetTopologySuite"] = "1";
    }
}
