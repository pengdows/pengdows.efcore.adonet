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
