using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore.TestUtilities;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Specification.Tests;

internal sealed class AdoNetPrecompiledQueryTestHelpers<THost> : PrecompiledQueryTestHelpers
    where THost : IDatabaseHost, new()
{
    public static AdoNetPrecompiledQueryTestHelpers<THost> Instance { get; } = new();

    private AdoNetPrecompiledQueryTestHelpers()
    {
    }

    protected override IEnumerable<MetadataReference> BuildProviderMetadataReferences()
    {
        yield return MetadataReference.CreateFromFile(typeof(AdoNetOptionsExtension).Assembly.Location);
        yield return MetadataReference.CreateFromFile(new THost().Dialect.GetType().Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(AdoNetPrecompiledQueryTestHelpers<THost>).Assembly.Location);
    }
}
