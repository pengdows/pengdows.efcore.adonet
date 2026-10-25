using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace pengdows.efcore.adonet.Update;

internal class AdoNetModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
    : AffectedCountModificationCommandBatch(dependencies, maxBatchSize);

/// <summary>
///     Commands whose affected row count is read from <see cref="System.Data.Common.DbDataReader.RecordsAffected" />
///     (valid after the reader is closed) rather than from a result set. The batch size is one for providers that cannot
///     execute multiple statements and larger when they explicitly support it.
/// </summary>
internal class AdoNetRecordsAffectedModificationCommandBatch(
    ModificationCommandBatchFactoryDependencies dependencies,
    int maxBatchSize)
    : AffectedCountModificationCommandBatch(dependencies, maxBatchSize)
{
    protected override void Consume(RelationalDataReader reader)
    {
        base.Consume(reader);

        if (RowsAffected(reader) is { } rowsAffected && rowsAffected != ModificationCommands.Count)
        {
            ThrowAggregateUpdateConcurrencyException(
                reader, commandIndex: ModificationCommands.Count, expectedRowsAffected: ModificationCommands.Count, rowsAffected);
        }
    }

    protected override async Task ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken = default)
    {
        await base.ConsumeAsync(reader, cancellationToken).ConfigureAwait(false);

        if (RowsAffected(reader) is { } rowsAffected && rowsAffected != ModificationCommands.Count)
        {
            await ThrowAggregateUpdateConcurrencyExceptionAsync(
                    reader, commandIndex: ModificationCommands.Count, expectedRowsAffected: ModificationCommands.Count, rowsAffected,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private int? RowsAffected(RelationalDataReader reader)
        => ResultSetMappings.All(mapping => mapping == ResultSetMapping.NoResults)
            ? reader.DbDataReader.RecordsAffected
            : null;
}

/// <summary>
///     Batches several modifications into one DbCommand only when the dialect says the driver accepts
///     multiple statements per command; otherwise every modification gets its own command.
/// </summary>
internal class AdoNetModificationCommandBatchFactory(
    ModificationCommandBatchFactoryDependencies dependencies,
    IDbContextOptions options,
    ISqlDialect sql,
    IDatabaseCapabilities capabilities)
    : IModificationCommandBatchFactory
{
    private readonly int _maxBatchSize = capabilities.SupportsMultipleStatementsPerCommand
        ? RelationalOptionsExtension.Extract(options).MaxBatchSize ?? capabilities.DefaultMaxBatchSize
        : 1;

    public virtual ModificationCommandBatch Create()
        => sql.Keys is SequenceKeyRetrieval && !capabilities.SupportsMultipleStatementsPerCommand
            ? new AdoNetRecordsAffectedModificationCommandBatch(dependencies, 1)
            : sql.Keys is SequenceKeyRetrieval
                ? new AdoNetRecordsAffectedModificationCommandBatch(dependencies, _maxBatchSize)
            : _maxBatchSize == 1
                ? new SingularModificationCommandBatch(dependencies)
                : new AdoNetModificationCommandBatch(dependencies, _maxBatchSize);
}
