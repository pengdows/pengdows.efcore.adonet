using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.mysql;

/// <summary>Gives EF Oracle's MySql.Data readers through <see cref="MySqlDataValueReader" />; other drivers' pass through.</summary>
internal sealed class MySqlDataValueReaderInterceptor : DbCommandInterceptor
{
    // Named, not referenced: MySql.Data is GPL, and an application chooses it.
    private const string MySqlDataReader = "MySql.Data.MySqlClient.MySqlDataReader";

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => Wrap(result);

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Wrap(result));

    private static DbDataReader Wrap(DbDataReader reader)
        => reader.GetType().FullName == MySqlDataReader ? new MySqlDataValueReader(reader) : reader;
}

/// <summary>
///     The values Oracle's MySql.Data reads as another CLR type than EF asks for, converted as MySqlConnector does:
///     <list type="bullet">
///         <item>
///             an integer: MySQL's CAST has no TINYINT or SMALLINT, so one computed in SQL (read out of JSON, say) arrives as
///             BIGINT or BIGINT UNSIGNED, and MySql.Data casts its own value type and throws (MySqlUInt64 to MySqlByte). It
///             is read as its boxed value, converted, so a value too big for the type still fails;
///         </item>
///         <item>a DATE as DateOnly and a TIME as TimeOnly, which MySql.Data reads only as DateTime and TimeSpan.</item>
///     </list>
/// </summary>
internal sealed class MySqlDataValueReader(DbDataReader reader) : DelegatingDataReader(reader)
{
    public override byte GetByte(int ordinal) => System.Convert.ToByte(Inner.GetValue(ordinal), CultureInfo.InvariantCulture);
    public override short GetInt16(int ordinal) => System.Convert.ToInt16(Inner.GetValue(ordinal), CultureInfo.InvariantCulture);
    public override int GetInt32(int ordinal) => System.Convert.ToInt32(Inner.GetValue(ordinal), CultureInfo.InvariantCulture);
    public override long GetInt64(int ordinal) => System.Convert.ToInt64(Inner.GetValue(ordinal), CultureInfo.InvariantCulture);

    public override T GetFieldValue<T>(int ordinal)
        => Converts(typeof(T)) ? ConvertTo<T>(Inner.GetValue(ordinal)) : Inner.GetFieldValue<T>(ordinal);

    public override async Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => Converts(typeof(T))
            ? ConvertTo<T>(await Inner.GetFieldValueAsync<object>(ordinal, cancellationToken).ConfigureAwait(false))
            : await Inner.GetFieldValueAsync<T>(ordinal, cancellationToken).ConfigureAwait(false);

    private static bool Converts(Type type)
        => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(DateOnly) || type == typeof(TimeOnly);

    private static T ConvertTo<T>(object value)
        => value switch
        {
            T same => same,
            DateTime dateTime when typeof(T) == typeof(DateOnly) => (T)(object)DateOnly.FromDateTime(dateTime),
            TimeSpan timeSpan when typeof(T) == typeof(TimeOnly) => (T)(object)TimeOnly.FromTimeSpan(timeSpan),
            _ => (T)System.Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture)
        };
}
