using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace pengdows.efcore.adonet.Update;

internal class AdoNetModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies, int maxBatchSize)
    : AffectedCountModificationCommandBatch(dependencies, maxBatchSize);

/// <summary>
///     One statement per command, whose affected row count is read from <see cref="System.Data.Common.DbDataReader.RecordsAffected" />
///     (valid after the reader is closed) rather than from a result set.
/// </summary>
internal class AdoNetRecordsAffectedModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies)
    : AffectedCountModificationCommandBatch(dependencies, maxBatchSize: 1)
{
    protected override void Consume(RelationalDataReader reader)
    {
        base.Consume(reader);

        if (RowsAffected(reader) is { } rowsAffected and not 1)
        {
            ThrowAggregateUpdateConcurrencyException(reader, commandIndex: 1, expectedRowsAffected: 1, rowsAffected);
        }
    }

    protected override async Task ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken = default)
    {
        await base.ConsumeAsync(reader, cancellationToken).ConfigureAwait(false);

        if (RowsAffected(reader) is { } rowsAffected and not 1)
        {
            await ThrowAggregateUpdateConcurrencyExceptionAsync(
                    reader, commandIndex: 1, expectedRowsAffected: 1, rowsAffected, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private int? RowsAffected(RelationalDataReader reader)
        => ResultSetMappings is [ResultSetMapping.NoResults] ? reader.DbDataReader.RecordsAffected : null;
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
        => sql.Keys is SequenceKeyRetrieval
            ? new AdoNetRecordsAffectedModificationCommandBatch(dependencies)
            : _maxBatchSize == 1
                ? new SingularModificationCommandBatch(dependencies)
                : new AdoNetModificationCommandBatch(dependencies, _maxBatchSize);
}
