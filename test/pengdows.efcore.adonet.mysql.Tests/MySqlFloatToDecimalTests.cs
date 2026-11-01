using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     .NET converts a float to decimal through the float's shortest decimal text, so Convert.ToDecimal(8.6f) is 8.6. MySQL's
///     CAST of a FLOAT to DECIMAL keeps the binary value (8.5999999046...), so the dialect converts through the text.
/// </summary>
public class MySqlFloatToDecimalTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private async Task<string?> Scalar(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task A_float_cast_to_decimal_through_its_text_is_the_shortest_decimal()
    {
        var value = await Scalar("SELECT CAST(CAST(CAST(8.6 AS FLOAT) AS CHAR) AS DECIMAL(65,30))");

        Assert.Equal(8.6m, decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_plain_cast_keeps_the_float_binary_value_so_it_differs_from_dotnet()
    {
        var value = await Scalar("SELECT CAST(CAST(8.6 AS FLOAT) AS DECIMAL(65,30))");

        Assert.NotEqual(8.6m, decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_float_concatenated_to_text_then_cast_to_decimal_is_the_same_shortest_decimal()
    {
        var value = await Scalar("SELECT CAST(CONCAT(CAST(8.6 AS FLOAT)) AS DECIMAL(65,30))");

        Assert.Equal(8.6m, decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture));
    }
}
