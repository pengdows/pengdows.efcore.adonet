using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     A byte[] row version a database keeps as a time it sets on every insert and update (a dialect whose
///     <see cref="pengdows.efcore.adonet.ITypeMappingDialect.RowVersionStoreType(System.Type)" /> for byte[] holds DateTime): the time's
///     <see cref="DateTime.ToBinary" /> as eight big-endian bytes, so later versions compare greater.
/// </summary>
internal sealed class RowVersionBytesConverter() : ValueConverter<byte[], DateTime>(v => FromBytes(v), v => ToBytes(v))
{
    public static readonly RowVersionBytesConverter Instance = new();

    public static readonly ValueComparer<byte[]> Comparer = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (hash, b) => HashCode.Combine(hash, b)),
        v => v.ToArray());

    private static byte[] ToBytes(DateTime value)
    {
        var bytes = BitConverter.GetBytes(value.ToBinary());
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    private static DateTime FromBytes(byte[] value)
    {
        var bytes = value.ToArray();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return DateTime.FromBinary(BitConverter.ToInt64(bytes));
    }
}
