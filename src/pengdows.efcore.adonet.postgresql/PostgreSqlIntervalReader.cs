using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NpgsqlTypes;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>Gives EF readers that read a TimeSpan out of an interval through <see cref="PostgreSqlIntervalReader" />.</summary>
internal sealed class PostgreSqlIntervalReaderInterceptor : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => Wrap(result);

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Wrap(result));

    private static DbDataReader Wrap(DbDataReader reader)
        => reader is PostgreSqlIntervalReader ? reader : new PostgreSqlIntervalReader(reader);
}

/// <summary>
///     The driver's reader, but a TimeSpan read out of an interval is read as Npgsql's NpgsqlInterval, and an interval holding
///     months, which have no fixed length, is refused. Npgsql's own TimeSpan reading refuses positive months but reads negative
///     ones as TimeSpan.Zero (https://github.com/npgsql/npgsql/pull/6682). Any other column, a time of day among them, reads as
///     Npgsql reads it. In the reader rather than the TimeSpan mapping: EF reads a column through its mapping before it knows
///     the column's type (DateTime.TimeOfDay is a time; raw SQL may return either), and buffers through the same call.
/// </summary>
internal sealed class PostgreSqlIntervalReader(DbDataReader reader) : DelegatingDataReader(reader)
{
    private const string Interval = "interval";

    public override T GetFieldValue<T>(int ordinal)
        => typeof(T) == typeof(TimeSpan) && IsInterval(ordinal)
            ? (T)(object)ToTimeSpan(Inner.GetFieldValue<NpgsqlInterval>(ordinal))
            : Inner.GetFieldValue<T>(ordinal);

    public override async Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => typeof(T) == typeof(TimeSpan) && IsInterval(ordinal)
            ? (T)(object)ToTimeSpan(await Inner.GetFieldValueAsync<NpgsqlInterval>(ordinal, cancellationToken))
            : await Inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override object GetValue(int ordinal)
        => IsInterval(ordinal) && !Inner.IsDBNull(ordinal)
            ? ToTimeSpan(Inner.GetFieldValue<NpgsqlInterval>(ordinal))
            : Inner.GetValue(ordinal);

    private bool IsInterval(int ordinal)
        => Inner.GetDataTypeName(ordinal) == Interval;

    private static TimeSpan ToTimeSpan(NpgsqlInterval interval)
        => interval.Months != 0
            ? throw new InvalidCastException(
                $"An interval of {interval.Months} months, {interval.Days} days and {interval.Time} microseconds has no TimeSpan: "
                + "months have no fixed length.")
            : TimeSpan.FromDays(interval.Days) + TimeSpan.FromTicks(interval.Time * (TimeSpan.TicksPerMillisecond / 1000));
}
