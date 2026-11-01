using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A ulong above long.MaxValue survives a trip through MySQL's JSON exactly: MySQL's JSON keeps the unsigned number, and the
///     dialect's BIGINT UNSIGNED column holds it, so the text read back is the value written. EF's provider-neutral spec expects
///     the signed wrap (-1) that a SQL Server bigint gives; that difference is attributed in the specification overrides.
/// </summary>
public class MySqlUnsignedJsonRoundTripTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_maximum_ulong_written_as_JSON_reads_back_as_the_same_unsigned_number()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(@value AS JSON)";
        command.Parameters.Add(new MySqlParameter("@value", MySqlDbType.UInt64) { Value = ulong.MaxValue });

        var json = Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("18446744073709551615", json);
    }

    [Fact]
    public async Task A_JSON_number_above_long_MaxValue_is_read_back_unsigned_by_a_ulong_column()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(JSON_EXTRACT(CAST('{\"Prop\":18446744073709551615}' AS JSON), '$.Prop') AS UNSIGNED)";

        var value = Convert.ToUInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(ulong.MaxValue, value);
    }

    [Fact]
    public async Task A_JSON_array_of_ulong_values_keeps_the_maximum_unsigned()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(JSON_EXTRACT(CAST(@value AS JSON), '$[0]') AS UNSIGNED)";
        command.Parameters.Add(new MySqlParameter("@value", "[18446744073709551615, 1]"));

        var value = Convert.ToUInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(ulong.MaxValue, value);
    }
}
