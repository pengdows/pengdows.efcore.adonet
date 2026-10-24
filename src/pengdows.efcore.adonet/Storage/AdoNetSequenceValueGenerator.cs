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
public class AdoNetSequenceValueGenerator<TValue>(
    string nextValueSql,
    IRelationalConnection connection,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IRelationalCommandDiagnosticsLogger commandLogger)
    : ValueGenerator<TValue>
{
    public override bool GeneratesTemporaryValues
        => false;

    public override TValue Next(EntityEntry entry)
        => Convert(rawSqlCommandBuilder.Build(nextValueSql).ExecuteScalar(Parameters(entry)));

    public override async ValueTask<TValue> NextAsync(EntityEntry entry, CancellationToken cancellationToken = default)
        => Convert(
            await rawSqlCommandBuilder.Build(nextValueSql)
                .ExecuteScalarAsync(Parameters(entry), cancellationToken)
                .ConfigureAwait(false));

    private RelationalCommandParameterObject Parameters(EntityEntry entry)
        => new(connection, null, null, entry.Context, commandLogger, CommandSource.ValueGenerator);

    private static TValue Convert(object? value)
        => (TValue)System.Convert.ChangeType(
            value ?? throw new InvalidOperationException("The sequence returned no value."),
            typeof(TValue),
            CultureInfo.InvariantCulture);
}
