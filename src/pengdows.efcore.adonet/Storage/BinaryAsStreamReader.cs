using System.Collections;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Gives EF readers that read binary values as <c>byte[]</c> where the driver hands them over only as a stream
///     (<see cref="IDatabaseCapabilities.ReadsBinaryAsStream" />); other drivers' readers pass through untouched.
/// </summary>
internal sealed class BinaryAsStreamReaderInterceptor(IDatabaseCapabilities capabilities) : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => Wrap(result);

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Wrap(result));

    private DbDataReader Wrap(DbDataReader reader)
        => capabilities.ReadsBinaryAsStream && reader is not BinaryAsStreamReader ? new BinaryAsStreamReader(reader) : reader;
}

/// <summary>
///     The driver's reader, whose binary values are copied out of the driver's stream while their row is current: the stream
///     may point into memory the driver reuses for the next rows.
/// </summary>
internal sealed class BinaryAsStreamReader(DbDataReader reader) : DelegatingDataReader(reader)
{
    public override T GetFieldValue<T>(int ordinal)
        => typeof(T) == typeof(byte[]) ? (T)(object)Bytes(ordinal) : Inner.GetFieldValue<T>(ordinal);

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => typeof(T) == typeof(byte[]) ? Task.FromResult((T)(object)Bytes(ordinal)) : Inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override object GetValue(int ordinal)
        => Inner.GetValue(ordinal) is Stream stream ? Copy(stream) : Inner.GetValue(ordinal);

    private byte[] Bytes(int ordinal)
        => Inner.GetValue(ordinal) switch
        {
            byte[] bytes => bytes,
            Stream stream => Copy(stream),
            var other => throw new InvalidCastException(
                $"Column {ordinal} holds a {other.GetType().Name}, which isn't binary.")
        };

    private static byte[] Copy(Stream stream)
    {
        using (stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}

/// <summary>
///     A driver's reader, every member passed through, for a provider that changes how some values are read. Values are read
///     through <see cref="GetValue" /> and <see cref="DbDataReader.GetFieldValue{T}" />, which a subclass overrides. Owns the
///     driver's reader, which it disposes.
/// </summary>
public abstract class DelegatingDataReader(DbDataReader reader) : DbDataReader
{
    protected DbDataReader Inner { get; } = reader;

    public override T GetFieldValue<T>(int ordinal)
        => Inner.GetFieldValue<T>(ordinal);

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => Inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override object GetValue(int ordinal)
        => Inner.GetValue(ordinal);

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override object this[int ordinal]
        => GetValue(ordinal);

    public override object this[string name]
        => GetValue(GetOrdinal(name));

    public override int Depth => Inner.Depth;
    public override int FieldCount => Inner.FieldCount;
    public override bool HasRows => Inner.HasRows;
    public override bool IsClosed => Inner.IsClosed;
    public override int RecordsAffected => Inner.RecordsAffected;
    public override int VisibleFieldCount => Inner.VisibleFieldCount;

    public override bool GetBoolean(int ordinal) => Inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => Inner.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => Inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => Inner.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => Inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => Inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => Inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => Inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => Inner.GetDouble(ordinal);
    public override Type GetFieldType(int ordinal) => Inner.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => Inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => Inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => Inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => Inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => Inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => Inner.GetName(ordinal);
    public override int GetOrdinal(string name) => Inner.GetOrdinal(name);
    public override string GetString(int ordinal) => Inner.GetString(ordinal);
    public override Stream GetStream(int ordinal) => Inner.GetStream(ordinal);
    public override TextReader GetTextReader(int ordinal) => Inner.GetTextReader(ordinal);
    public override bool IsDBNull(int ordinal) => Inner.IsDBNull(ordinal);
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Inner.IsDBNullAsync(ordinal, cancellationToken);
    public override System.Data.DataTable? GetSchemaTable() => Inner.GetSchemaTable();
    public override bool NextResult() => Inner.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Inner.NextResultAsync(cancellationToken);
    public override bool Read() => Inner.Read();
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Inner.ReadAsync(cancellationToken);
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
    public override void Close() => Inner.Close();
    public override Task CloseAsync() => Inner.CloseAsync();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
        => Inner.DisposeAsync();
}
