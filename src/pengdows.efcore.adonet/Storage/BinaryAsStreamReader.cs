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
///     may point into memory the driver reuses for the next rows. Owns the driver's reader, which it disposes.
/// </summary>
internal sealed class BinaryAsStreamReader(DbDataReader reader) : DbDataReader
{
    public override T GetFieldValue<T>(int ordinal)
        => typeof(T) == typeof(byte[]) ? (T)(object)Bytes(ordinal) : reader.GetFieldValue<T>(ordinal);

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => typeof(T) == typeof(byte[]) ? Task.FromResult((T)(object)Bytes(ordinal)) : reader.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override object GetValue(int ordinal)
        => reader.GetValue(ordinal) is Stream stream ? Copy(stream) : reader.GetValue(ordinal);

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    private byte[] Bytes(int ordinal)
        => reader.GetValue(ordinal) switch
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

    public override object this[int ordinal]
        => GetValue(ordinal);

    public override object this[string name]
        => GetValue(GetOrdinal(name));

    public override int Depth => reader.Depth;
    public override int FieldCount => reader.FieldCount;
    public override bool HasRows => reader.HasRows;
    public override bool IsClosed => reader.IsClosed;
    public override int RecordsAffected => reader.RecordsAffected;
    public override int VisibleFieldCount => reader.VisibleFieldCount;

    public override bool GetBoolean(int ordinal) => reader.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => reader.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => reader.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => reader.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => reader.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => reader.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => reader.GetDouble(ordinal);
    public override Type GetFieldType(int ordinal) => reader.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => reader.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => reader.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => reader.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => reader.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => reader.GetInt64(ordinal);
    public override string GetName(int ordinal) => reader.GetName(ordinal);
    public override int GetOrdinal(string name) => reader.GetOrdinal(name);
    public override string GetString(int ordinal) => reader.GetString(ordinal);
    public override Stream GetStream(int ordinal) => reader.GetStream(ordinal);
    public override TextReader GetTextReader(int ordinal) => reader.GetTextReader(ordinal);
    public override bool IsDBNull(int ordinal) => reader.IsDBNull(ordinal);
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => reader.IsDBNullAsync(ordinal, cancellationToken);
    public override System.Data.DataTable? GetSchemaTable() => reader.GetSchemaTable();
    public override bool NextResult() => reader.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => reader.NextResultAsync(cancellationToken);
    public override bool Read() => reader.Read();
    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => reader.ReadAsync(cancellationToken);
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
    public override void Close() => reader.Close();
    public override Task CloseAsync() => reader.CloseAsync();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            reader.Dispose();
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
        => reader.DisposeAsync();
}
