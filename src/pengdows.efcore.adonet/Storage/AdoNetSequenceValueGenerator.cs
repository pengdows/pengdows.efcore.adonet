using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Fetches a permanent key from a database sequence before the INSERT, for databases that cannot return
///     generated values. One round trip per new entity; a hi-lo block allocator would be the next step.
/// </summary>
internal class AdoNetSequenceValueGenerator<TValue>(
    string nextValueSql,
    IRelationalConnection connection,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IRelationalCommandDiagnosticsLogger commandLogger)
    : ValueGenerator<TValue>
{
    public override bool GeneratesTemporaryValues
        => false;

    // A key is drawn when the entity is added, outside SaveChanges and so outside its execution strategy: the draw runs
    // under the context's own, so a retrying one retries it. Not inside the application's transaction, where a retrying
    // strategy refuses to run (a failed statement may have aborted the transaction); a skipped sequence value is harmless.
    public override TValue Next(EntityEntry entry)
    {
        var strategy = Strategy(entry);
        return Convert(
            strategy == null
                ? Draw(entry)
                : strategy.Execute(entry, Draw));
    }

    public override async ValueTask<TValue> NextAsync(EntityEntry entry, CancellationToken cancellationToken = default)
    {
        var strategy = Strategy(entry);
        return Convert(
            strategy == null
                ? await DrawAsync(entry, cancellationToken).ConfigureAwait(false)
                : await strategy.ExecuteAsync(entry, DrawAsync, cancellationToken).ConfigureAwait(false));
    }

    private static IExecutionStrategy? Strategy(EntityEntry entry)
        => entry.Context.Database.CurrentTransaction == null
            && entry.Context.Database.CreateExecutionStrategy() is { RetriesOnFailure: true } strategy
                ? strategy
                : null;

    private object? Draw(EntityEntry entry)
        => rawSqlCommandBuilder.Build(nextValueSql).ExecuteScalar(Parameters(entry));

    private Task<object?> DrawAsync(EntityEntry entry, CancellationToken cancellationToken)
        => rawSqlCommandBuilder.Build(nextValueSql).ExecuteScalarAsync(Parameters(entry), cancellationToken);

    private RelationalCommandParameterObject Parameters(EntityEntry entry)
        => new(connection, null, null, entry.Context, commandLogger, CommandSource.ValueGenerator);

    private static TValue Convert(object? value)
        => (TValue)System.Convert.ChangeType(
            value ?? throw new InvalidOperationException("The sequence returned no value."),
            typeof(TValue),
            CultureInfo.InvariantCulture);
}
