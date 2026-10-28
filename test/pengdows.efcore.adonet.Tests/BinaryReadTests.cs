using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A driver that hands binary values back only as a stream (DuckDB.NET 1.5.6.1 throws InvalidCastException for
///     GetFieldValue&lt;byte[]&gt; on a BLOB, Giorgi/DuckDB.NET#384) has them read through DbDataReader.GetStream and copied
///     out; any driver reads them the same either way.
/// </summary>
public class BinaryReadTests
{
    public static TheoryData<bool, byte[]> Values
        => new()
        {
            { true, [] },
            { true, [0, 1, 255] },
            { true, Enumerable.Range(0, 65_537).Select(i => (byte)i).ToArray() },
            { false, [0, 1, 255] }
        };

    [Theory]
    [MemberData(nameof(Values))]
    public async Task Binary_values_read_back_whole_however_the_driver_hands_them_over(bool readsBinaryAsStream, byte[] value)
    {
        var dialect = new PartsDialect(
            $"Binary as stream {readsBinaryAsStream}", capabilities: new DatabaseCapabilities { ReadsBinaryAsStream = readsBinaryAsStream });
        var database = new FakeDatabase();
        await using var context = database.ContextOf<BlobContext>(dialect, o => new BlobContext(o));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 1, ["Data"] = value });

        var blob = await context.Blobs.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(value, blob.Data);
    }

    public sealed class Blob
    {
        public int Id { get; set; }
        public byte[] Data { get; set; } = [];
    }

    public sealed class BlobContext(DbContextOptions<BlobContext> options) : DbContext(options)
    {
        public DbSet<Blob> Blobs => Set<Blob>();
    }
}
