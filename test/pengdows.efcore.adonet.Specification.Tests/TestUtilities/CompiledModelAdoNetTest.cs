using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.TestUtilities;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
/// Adds the provider assemblies used by generated compiled-model source to EF's in-memory compiler.
/// </summary>
internal static class CompiledModelAdoNetReferences
{
    public static BuildSource AddProviderReferences<THost>(BuildSource build)
        where THost : IDatabaseHost, new()
    {
        build.References.Add(BuildReference.ByName(typeof(AdoNetStringTypeMapping).Assembly.GetName().Name!));
        build.References.Add(BuildReference.ByName(typeof(THost).Assembly.GetName().Name!));
        build.References.Add(BuildReference.ByName(new THost().Dialect.GetType().Assembly.GetName().Name!));
        return build;
    }
}
