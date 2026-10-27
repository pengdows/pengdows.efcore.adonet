using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

#pragma warning disable EF9100 // EF's precompiled-query opt-in constructor is the provider extension point.

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     Buffers a split query's results where a connection holds one open reader at a time
///     (<see cref="IDatabaseCapabilities.SupportsMultipleActiveReaders" />), so each of its commands runs on a connection
///     whose previous reader is read and closed.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQueryCompilationContext.cs
/// </remarks>
internal sealed class AdoNetQueryCompilationContext(
    QueryCompilationContextDependencies dependencies,
    RelationalQueryCompilationContextDependencies relationalDependencies,
    bool async,
    bool precompiling,
    bool multipleActiveReaders)
    : RelationalQueryCompilationContext(dependencies, relationalDependencies, async, precompiling)
{
    public override bool SupportsPrecompiledQuery => true;

    public override bool IsBuffering
        => base.IsBuffering || ((QuerySplittingBehavior == Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery) && !multipleActiveReaders);
}

internal sealed class AdoNetQueryCompilationContextFactory(
    QueryCompilationContextDependencies dependencies,
    RelationalQueryCompilationContextDependencies relationalDependencies,
    IDatabaseCapabilities capabilities)
    : IQueryCompilationContextFactory
{
    public QueryCompilationContext Create(bool async)
        => new AdoNetQueryCompilationContext(dependencies, relationalDependencies, async, false, capabilities.SupportsMultipleActiveReaders);

    public QueryCompilationContext CreatePrecompiled(bool async)
        => new AdoNetQueryCompilationContext(dependencies, relationalDependencies, async, true, capabilities.SupportsMultipleActiveReaders);
}

#pragma warning restore EF9100
