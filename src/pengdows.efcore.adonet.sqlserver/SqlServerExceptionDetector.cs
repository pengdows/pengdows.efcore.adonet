using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>Recognizes both driver cancellation exceptions and an already-canceled command token.</summary>
internal sealed class SqlServerExceptionDetector : IExceptionDetector
{
    public bool IsCancellation(Exception exception, CancellationToken cancellationToken = default)
        => exception is OperationCanceledException || cancellationToken.IsCancellationRequested;
}
