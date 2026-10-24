using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Savepoints go through <see cref="DbTransaction.Save" />, <see cref="DbTransaction.Rollback(string)" /> and
///     <see cref="DbTransaction.Release" /> instead of SQL text, so each provider uses its own syntax (or reports
///     <see cref="DbTransaction.SupportsSavepoints" /> = false, in which case EF skips savepoints entirely).
///     Logging and interception match EF's base implementation.
/// </summary>
public class AdoNetRelationalTransaction(
    IRelationalConnection connection,
    DbTransaction transaction,
    Guid transactionId,
    IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
    bool transactionOwned,
    ISqlGenerationHelper sqlGenerationHelper)
    : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
{
    private DbTransaction DbTransaction
        => this.GetDbTransaction();

    public override bool SupportsSavepoints
        => DbTransaction.SupportsSavepoints;

    public override void CreateSavepoint(string name)
        => Execute(
            "CreateSavepoint",
            start => Logger.CreatingTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => DbTransaction.Save(name),
            start => Logger.CreatedTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "CreateSavepoint",
            start => Logger.CreatingTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => DbTransaction.SaveAsync(name, cancellationToken),
            start => Logger.CreatedTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    public override void RollbackToSavepoint(string name)
        => Execute(
            "RollbackToSavepoint",
            start => Logger.RollingBackToTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => DbTransaction.Rollback(name),
            start => Logger.RolledBackToTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "RollbackToSavepoint",
            start => Logger.RollingBackToTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => DbTransaction.RollbackAsync(name, cancellationToken),
            start => Logger.RolledBackToTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    public override void ReleaseSavepoint(string name)
        => Execute(
            "ReleaseSavepoint",
            start => Logger.ReleasingTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => DbTransaction.Release(name),
            start => Logger.ReleasedTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "ReleaseSavepoint",
            start => Logger.ReleasingTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => DbTransaction.ReleaseAsync(name, cancellationToken),
            start => Logger.ReleasedTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    private void Execute(
        string action,
        Func<DateTimeOffset, InterceptionResult> before,
        Action operation,
        Action<DateTimeOffset> after)
    {
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!before(startTime).IsSuppressed)
            {
                operation();
            }

            after(startTime);
        }
        catch (Exception e)
        {
            Logger.TransactionError(Connection, DbTransaction, TransactionId, action, e, startTime, stopwatch.Elapsed);
            throw;
        }
    }

    private async Task ExecuteAsync(
        string action,
        Func<DateTimeOffset, ValueTask<InterceptionResult>> before,
        Func<Task> operation,
        Func<DateTimeOffset, Task> after)
    {
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!(await before(startTime).ConfigureAwait(false)).IsSuppressed)
            {
                await operation().ConfigureAwait(false);
            }

            await after(startTime).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await Logger.TransactionErrorAsync(Connection, DbTransaction, TransactionId, action, e, startTime, stopwatch.Elapsed)
                .ConfigureAwait(false);
            throw;
        }
    }
}

public class AdoNetRelationalTransactionFactory(RelationalTransactionFactoryDependencies dependencies)
    : RelationalTransactionFactory(dependencies)
{
    public override RelationalTransaction Create(
        IRelationalConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
        bool transactionOwned)
        => new AdoNetRelationalTransaction(
            connection, transaction, transactionId, logger, transactionOwned, Dependencies.SqlGenerationHelper);
}
