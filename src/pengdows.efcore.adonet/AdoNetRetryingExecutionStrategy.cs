using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet;

/// <summary>
///     Retries an operation that failed for a reason the database's capabilities call transient
///     (<see cref="IDatabaseCapabilities.IsTransient" />), with EF's exponential back-off. Enabled with
///     <c>EnableRetryOnFailure</c>; subclass it to change the delay or what is retried.
/// </summary>
/// <remarks>
///     One strategy for every database: EF's server providers each have their own, differing only in which
///     errors they call transient. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/SqlServerRetryingExecutionStrategy.cs
/// </remarks>
public class AdoNetRetryingExecutionStrategy : ExecutionStrategy
{
    private readonly IDatabaseCapabilities _capabilities;
    private readonly Func<Exception, bool>? _alsoRetryOn;

    public AdoNetRetryingExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        IDatabaseCapabilities capabilities,
        int maxRetryCount = 6,
        TimeSpan? maxRetryDelay = null,
        Func<Exception, bool>? alsoRetryOn = null)
        : base(dependencies, maxRetryCount, maxRetryDelay ?? DefaultMaxDelay)
    {
        _capabilities = capabilities;
        _alsoRetryOn = alsoRetryOn;
    }

    protected override bool ShouldRetryOn(Exception exception)
        => _capabilities.IsTransient(exception) || (_alsoRetryOn?.Invoke(exception) ?? false);
}
