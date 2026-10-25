namespace pengdows.efcore.adonet.duckdb.Tests;

public class DuckDbLiteralTests
{
    [Theory]
    [InlineData(new byte[] { 0, 1, 255 }, @"'\x00\x01\xFF'::BLOB")]
    [InlineData(new byte[0], "''::BLOB")]
    public void Blobs_are_escaped_bytes(byte[] value, string literal)
        => Assert.Equal(literal, DuckDbDialect.Instance.TypeMappings.FormatLiteral(value, "BLOB", "unused"));

    [Fact]
    public void Date_offsets_keep_their_offset()
        => Assert.Equal(
            "TIMESTAMPTZ '2024-03-05 01:00:00.5+02:00'",
            DuckDbDialect.Instance.TypeMappings.FormatLiteral(
                new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, TimeSpan.FromHours(2)), "TIMESTAMP WITH TIME ZONE", "unused"));
}
