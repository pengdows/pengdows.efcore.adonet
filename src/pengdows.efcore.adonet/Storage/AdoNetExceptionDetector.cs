using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     A failure once the caller's token is canceled is the cancellation, whatever exception the driver reports it in.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerExceptionDetector.cs
/// </summary>
internal sealed class AdoNetExceptionDetector : IExceptionDetector
{
    public bool IsCancellation(Exception exception, CancellationToken cancellationToken = default)
        => exception is OperationCanceledException || cancellationToken.IsCancellationRequested;
}
