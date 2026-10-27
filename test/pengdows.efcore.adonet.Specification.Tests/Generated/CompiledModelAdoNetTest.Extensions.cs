using Microsoft.EntityFrameworkCore.TestUtilities;

namespace pengdows.efcore.adonet.Specification.Tests;

public abstract partial class CompiledModelAdoNetTest<THost>
{
    protected override BuildSource AddReferences(
        BuildSource build,
        [System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
        => CompiledModelAdoNetReferences.AddProviderReferences<THost>(base.AddReferences(build, filePath));
}
