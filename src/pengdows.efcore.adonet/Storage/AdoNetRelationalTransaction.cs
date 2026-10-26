using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Whether the database has savepoints is the dialect's to say: <see cref="DbTransaction.SupportsSavepoints" /> says
///     whether the driver implements the methods, not whether the server keeps them (a driver may report them on a server
///     that accepts the statements and ignores them). Where the dialect has them, they go through
///     <see cref="DbTransaction.Save" />, <see cref="DbTransaction.Rollback(string)" /> and <see cref="DbTransaction.Release" />
///     if the driver offers them, so it uses its own syntax, and otherwise through the dialect's <see cref="SavepointSyntax" />,
///     as commands in the transaction. Where it has none, all three throw <see cref="NotSupportedException" />, as
///     <see cref="DbTransaction" /> specifies when savepoints aren't supported, and EF skips them. Logging and interception
///     match EF's base implementation.
/// </summary>
internal class AdoNetRelationalTransaction(
    IRelationalConnection connection,
    DbTransaction transaction,
    Guid transactionId,
    IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
    bool transactionOwned,
    ISqlGenerationHelper sqlGenerationHelper,
    IAdoNetDialect dialect)
    : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
{
    private readonly ISqlGenerationHelper _sqlGenerationHelper = sqlGenerationHelper;

    private DbTransaction DbTransaction
        => this.GetDbTransaction();

    // The driver's savepoints, or else the dialect's SQL; only called where the dialect has savepoints.
    private SavepointSyntax? Sql
        => DbTransaction.SupportsSavepoints ? null : dialect.Sql.Savepoints;

    public override bool SupportsSavepoints
        => dialect.Sql.Savepoints != null;

    public override void CreateSavepoint(string name)
        => Execute(
            "CreateSavepoint",
            start => Logger.CreatingTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => Run(Sql?.Create, name, () => DbTransaction.Save(name)),
            start => Logger.CreatedTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "CreateSavepoint",
            start => Logger.CreatingTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => RunAsync(Sql?.Create, name, () => DbTransaction.SaveAsync(name, cancellationToken), cancellationToken),
            start => Logger.CreatedTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    public override void RollbackToSavepoint(string name)
        => Execute(
            "RollbackToSavepoint",
            start => Logger.RollingBackToTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => Run(Sql?.RollbackTo, name, () => DbTransaction.Rollback(name)),
            start => Logger.RolledBackToTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "RollbackToSavepoint",
            start => Logger.RollingBackToTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => RunAsync(Sql?.RollbackTo, name, () => DbTransaction.RollbackAsync(name, cancellationToken), cancellationToken),
            start => Logger.RolledBackToTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    public override void ReleaseSavepoint(string name)
        => Execute(
            "ReleaseSavepoint",
            start => Logger.ReleasingTransactionSavepoint(Connection, DbTransaction, TransactionId, start),
            () => Run(Sql == null ? null : (Sql.Release ?? ""), name, () => DbTransaction.Release(name)),
            start => Logger.ReleasedTransactionSavepoint(Connection, DbTransaction, TransactionId, start));

    public override Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "ReleaseSavepoint",
            start => Logger.ReleasingTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken),
            () => RunAsync(Sql == null ? null : (Sql.Release ?? ""), name, () => DbTransaction.ReleaseAsync(name, cancellationToken), cancellationToken),
            start => Logger.ReleasedTransactionSavepointAsync(Connection, DbTransaction, TransactionId, start, cancellationToken));

    // A null template is the driver's call; an empty one, nothing to do; otherwise the statement, through EF's command
    // pipeline (logged and intercepted like any command) on this connection, which enlists it in this transaction.
    private void Run(string? template, string name, Action driver)
    {
        RequireSavepoints();
        if (template == null)
        {
            driver();
            return;
        }

        if (template.Length > 0)
        {
            Command(template, name).ExecuteNonQuery(Parameters());
        }
    }

    private Task RunAsync(string? template, string name, Func<Task> driver, CancellationToken cancellationToken)
    {
        RequireSavepoints();
        if (template == null)
        {
            return driver();
        }

        return template.Length > 0 ? Command(template, name).ExecuteNonQueryAsync(Parameters(), cancellationToken) : Task.CompletedTask;
    }

    private void RequireSavepoints()
    {
        if (!SupportsSavepoints)
        {
            throw new NotSupportedException($"{dialect.Name} has no savepoints.");
        }
    }

    private IRelationalCommand Command(string template, string name)
        => Connection.Context!.GetService<IRawSqlCommandBuilder>()
            .Build(template.Replace("{0}", _sqlGenerationHelper.DelimitIdentifier(name), StringComparison.Ordinal));

    private RelationalCommandParameterObject Parameters()
        => new(Connection, null, null, Connection.Context, Connection.Context!.GetService<IRelationalCommandDiagnosticsLogger>(), CommandSource.Unknown);

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

internal class AdoNetRelationalTransactionFactory(RelationalTransactionFactoryDependencies dependencies, IAdoNetDialect dialect)
    : RelationalTransactionFactory(dependencies)
{
    public override RelationalTransaction Create(
        IRelationalConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger,
        bool transactionOwned)
        => new AdoNetRelationalTransaction(
            connection, transaction, transactionId, logger, transactionOwned, Dependencies.SqlGenerationHelper, dialect);
}
