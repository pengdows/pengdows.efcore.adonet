using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Values Oracle's MySql.Data reads as another CLR type than EF asks for, read as MySqlConnector reads them. MySQL's CAST
///     has no TINYINT or SMALLINT, so an integer read out of JSON, or any expression, arrives as BIGINT or BIGINT UNSIGNED
///     whatever CLR type EF reads it as: MySql.Data casts its own value type and throws (MySqlUInt64 to MySqlByte). And it
///     reads DATE and TIME only as DateTime and TimeSpan, not DateOnly and TimeOnly.
///     Provider-only: the drivers are MySQL's; run against both.
/// </summary>
public abstract class MySqlValueReadTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : MySqlFixture
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private async Task<T> Read<T>(string sql, bool buffered)
    {
        await using var context = buffered
            ? fixture.CreateContext<BlogContext>(o => new BlogContext(o), o => o.EnableRetryOnFailure())
            : fixture.CreateContext();
        return await context.Database.SqlQueryRaw<T>(sql).SingleAsync(Ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wide_integers_read_as_narrower_clr_types(bool buffered)
    {
        Assert.Equal((byte)200, await Read<byte>("SELECT CAST(200 AS UNSIGNED) AS `Value`", buffered));
        Assert.Equal((short)-300, await Read<short>("SELECT CAST(-300 AS SIGNED) AS `Value`", buffered));
        Assert.Equal(70000, await Read<int>("SELECT CAST(70000 AS UNSIGNED) AS `Value`", buffered));
        Assert.Equal((sbyte)-5, await Read<sbyte>("SELECT CAST(-5 AS SIGNED) AS `Value`", buffered));
        Assert.Equal((ushort)60000, await Read<ushort>("SELECT CAST(60000 AS UNSIGNED) AS `Value`", buffered));
        Assert.Equal(5_000_000_000L, await Read<long>("SELECT CAST(5000000000 AS UNSIGNED) AS `Value`", buffered));
    }

    // MySql.Data reads DATE as DateTime and TIME as TimeSpan, which it doesn't convert to DateOnly or TimeOnly.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dates_and_times_read_as_date_only_and_time_only(bool buffered)
    {
        Assert.Equal(new DateOnly(2026, 10, 28), await Read<DateOnly>("SELECT CAST('2026-10-28' AS DATE) AS `Value`", buffered));
        Assert.Equal(new TimeOnly(13, 14, 15), await Read<TimeOnly>("SELECT CAST('13:14:15' AS TIME) AS `Value`", buffered));
    }

    [Fact]
    public async Task A_value_too_big_for_the_clr_type_still_fails()
        => await Assert.ThrowsAnyAsync<Exception>(() => Read<byte>("SELECT CAST(300 AS UNSIGNED) AS `Value`", buffered: false));
}

public class MySqlConnectorValueReadTests(MySqlFixture fixture) : MySqlValueReadTests<MySqlFixture>(fixture);

public class MySqlDataValueReadTests(MySqlDataFixture fixture) : MySqlValueReadTests<MySqlDataFixture>(fixture);
