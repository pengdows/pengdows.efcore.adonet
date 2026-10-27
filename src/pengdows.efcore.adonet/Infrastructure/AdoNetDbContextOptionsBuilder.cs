using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Infrastructure;

public class AdoNetDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    : RelationalDbContextOptionsBuilder<AdoNetDbContextOptionsBuilder, AdoNetOptionsExtension>(optionsBuilder)
{
    public virtual AdoNetDbContextOptionsBuilder UseDialect(IAdoNetDialect dialect)
        => WithOption(e => e.WithDialect(dialect));

    /// <summary>
    ///     Writes <c>NULLS FIRST</c> (ascending) or <c>NULLS LAST</c> (descending) on every ORDER BY, so nulls sort as in .NET and
    ///     SQL Server (Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQuerySqlGenerator.cs): for a database whose own default is the other way, and that takes the syntax (PostgreSQL, DuckDB). EF's
    ///     specification tests assume that order. Not public, as in the providers it follows: indexes would also need to be
    ///     created with the matching order for such a sort to use them.
    /// </summary>
    internal virtual AdoNetDbContextOptionsBuilder ReverseNullOrdering(bool reverseNullOrdering = true)
        => WithOption(e => e.WithReverseNullOrdering(reverseNullOrdering));

    /// <summary>
    ///     Retries operations that fail for a transient reason (as the dialect's <see cref="IDatabaseCapabilities.IsTransient" />
    ///     says, plus <paramref name="alsoRetryOn" />), up to <paramref name="maxRetryCount" /> times, waiting at most
    ///     <paramref name="maxRetryDelay" /> (30 seconds by default) between attempts.
    /// </summary>
    public virtual AdoNetDbContextOptionsBuilder EnableRetryOnFailure(
        int maxRetryCount = 6,
        TimeSpan? maxRetryDelay = null,
        Func<Exception, bool>? alsoRetryOn = null)
        => ExecutionStrategy(
            d => new AdoNetRetryingExecutionStrategy(
                d, d.CurrentContext.Context.GetService<IAdoNetDialect>().Capabilities, maxRetryCount, maxRetryDelay, alsoRetryOn));
}
