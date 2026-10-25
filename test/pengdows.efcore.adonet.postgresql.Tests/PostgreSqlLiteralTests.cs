namespace pengdows.efcore.adonet.postgresql.Tests;

public class PostgreSqlLiteralTests
{
    public static TheoryData<object, string, string> Literals => new()
    {
        { new byte[] { 0, 1, 255 }, "BYTEA", @"'\x0001FF'::bytea" },
        { Array.Empty<byte>(), "BYTEA", @"'\x'::bytea" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, 500, TimeSpan.FromHours(2)), "TIMESTAMP WITH TIME ZONE", "TIMESTAMPTZ '2024-03-05 01:00:00.5+02:00'" },
        { new DateTimeOffset(2024, 3, 5, 1, 0, 0, TimeSpan.FromHours(-5.5)), "TIMESTAMP WITH TIME ZONE", "TIMESTAMPTZ '2024-03-05 01:00:00-05:30'" },
        { new TimeSpan(0, 1, 2, 3, 500), "INTERVAL", "INTERVAL '3723500000 microseconds'" },
        { new TimeSpan(2, 1, 0, 0), "INTERVAL", "INTERVAL '176400000000 microseconds'" },
        { -new TimeSpan(1, 1, 30, 0), "INTERVAL", "INTERVAL '-91800000000 microseconds'" }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Writes_values_postgresql_reads_as_the_column_type(object value, string storeType, string literal)
        => Assert.Equal(literal, PostgreSqlDialect.Instance.TypeMappings.FormatLiteral(value, storeType, "unused"));
}
