using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Storage;

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
    IRelationalConnection connection)
    : IQueryCompilationContextFactory
{
    public QueryCompilationContext Create(bool async)
        => new AdoNetQueryCompilationContext(dependencies, relationalDependencies, async, false, MultipleActiveReaders(connection));

    public QueryCompilationContext CreatePrecompiled(bool async)
        => new AdoNetQueryCompilationContext(dependencies, relationalDependencies, async, true, MultipleActiveReaders(connection));

    // A dialect's own connection service can't say, so its split queries buffer: the safe assumption.
    internal static bool MultipleActiveReaders(IRelationalConnection connection)
        => connection is AdoNetRelationalConnection { SupportsMultipleActiveReaders: true };
}

/// <summary>
///     A query compiled to stream a split query's collections isn't reused on a connection that buffers them, or the reverse.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerCompiledQueryCacheKeyGenerator.cs
/// </summary>
internal sealed class AdoNetCompiledQueryCacheKeyGenerator(
    CompiledQueryCacheKeyGeneratorDependencies dependencies,
    RelationalCompiledQueryCacheKeyGeneratorDependencies relationalDependencies,
    IRelationalConnection connection)
    : RelationalCompiledQueryCacheKeyGenerator(dependencies, relationalDependencies)
{
    public override object GenerateCacheKey(System.Linq.Expressions.Expression query, bool async)
        => new CacheKey(GenerateCacheKeyCore(query, async), AdoNetQueryCompilationContextFactory.MultipleActiveReaders(connection));

    private readonly record struct CacheKey(RelationalCompiledQueryCacheKey Relational, bool MultipleActiveReaders);
}

#pragma warning restore EF9100
