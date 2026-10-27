// Portions adapted from Pomelo's MySqlModificationCommandBatch.
// Copyright (c) Pomelo Foundation. Licensed under the MIT license.

using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace pengdows.efcore.adonet.Update;

internal class AdoNetModificationCommandBatch(
    ModificationCommandBatchFactoryDependencies dependencies,
    int maxBatchSize,
    int maxParametersPerCommand,
    ISqlDialect sql)
    : AffectedCountModificationCommandBatch(dependencies, maxBatchSize)
{
    private readonly int _maxParametersPerCommand = maxParametersPerCommand;

    public override bool TryAddCommand(IReadOnlyModificationCommand modificationCommand)
    {
        var added = base.TryAddCommand(modificationCommand);
        if (!added || (RelationalCommandBuilder.Parameters.Count <= _maxParametersPerCommand) || (ModificationCommands.Count == 1))
        {
            return added;
        }

        RollbackLastCommand(modificationCommand);
        return false;
    }

    // Where a procedure's outputs go through session variables (ISqlDialect.ProcedureOutputVariable), the command has no
    // output parameters: an output-only one isn't sent, and an input-output one is sent as an input, the variable's start.
    // Adapted from Pomelo's MySqlModificationCommandBatch (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Update/Internal/MySqlModificationCommandBatch.cs
    protected override void AddParameter(IColumnModification columnModification)
    {
        if ((sql.ProcedureOutputVariable == null) || !AdoNetModificationCommand.IsOutput(columnModification))
        {
            base.AddParameter(columnModification);
            return;
        }

        if (columnModification.Column is not IStoreStoredProcedureParameter { Direction: ParameterDirection.InputOutput })
        {
            return;
        }

        // EF adds the one parameter, its original value's where it has one, as input-output; it is replaced by an input.
        base.AddParameter(columnModification);
        var index = RelationalCommandBuilder.Parameters.Count - 1;
        var name = RelationalCommandBuilder.Parameters[index].InvariantName;
        RelationalCommandBuilder.RemoveParameterAt(index);
        RelationalCommandBuilder.AddParameter(
            name,
            Dependencies.SqlGenerationHelper.GenerateParameterName(name),
            columnModification.TypeMapping!,
            columnModification.IsNullable,
            ParameterDirection.Input);
    }
}

/// <summary>
///     Commands whose affected row count is read from <see cref="System.Data.Common.DbDataReader.RecordsAffected" />
///     (valid after the reader is closed) rather than from a result set. The batch size is one for providers that cannot
///     execute multiple statements and larger when they explicitly support it.
/// </summary>
internal class AdoNetRecordsAffectedModificationCommandBatch(
    ModificationCommandBatchFactoryDependencies dependencies,
    int maxBatchSize,
    int maxParametersPerCommand,
    ISqlDialect sql)
    : AdoNetModificationCommandBatch(dependencies, maxBatchSize, maxParametersPerCommand, sql)
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
    private readonly int _maxParametersPerCommand = capabilities.MaxParametersPerCommand;

    public virtual ModificationCommandBatch Create()
        => sql.Keys is SequenceKeyRetrieval
            ? new AdoNetRecordsAffectedModificationCommandBatch(dependencies, _maxBatchSize, _maxParametersPerCommand, sql)
            : new AdoNetModificationCommandBatch(dependencies, _maxBatchSize, _maxParametersPerCommand, sql);
}
