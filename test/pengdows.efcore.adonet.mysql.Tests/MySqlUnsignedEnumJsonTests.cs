using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

public enum UnsignedEnumValue : ulong
{
    Min = 0,
    One = 1,
    Max = ulong.MaxValue
}

public sealed class UnsignedJsonPayload
{
    public UnsignedEnumValue Prop { get; set; }
}

public sealed class UnsignedNullableJsonPayload
{
    public UnsignedEnumValue? Prop { get; set; }
}

public sealed class UnsignedJsonRow
{
    public int Id { get; set; }
    public UnsignedJsonPayload Payload { get; set; } = new();
    public UnsignedNullableJsonPayload NullablePayload { get; set; } = new();
}

public sealed class UnsignedJsonContext(DbContextOptions<UnsignedJsonContext> options) : DbContext(options)
{
    public DbSet<UnsignedJsonRow> Rows => Set<UnsignedJsonRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnsignedJsonRow>(b =>
        {
            b.ToTable("UnsignedJsonRows");
            b.HasKey(r => r.Id);
            b.OwnsOne(r => r.Payload, p => p.ToJson("Payload"));
            b.OwnsOne(r => r.NullablePayload, p => p.ToJson("NullablePayload"));
        });
    }
}

/// <summary>
///     A ulong enum in an owned JSON document, written and read back through EF on MySQL: 0, 1, null and the maximum ulong,
///     each exact. The specification suite's theories for these values expect the signed wrap (-1) of a SQL Server bigint for
///     the maximum; this suite is the coverage that keeps the exact unsigned behaviour.
/// </summary>
public class MySqlUnsignedEnumJsonTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private UnsignedJsonContext NewContext()
        => new(new DbContextOptionsBuilder<UnsignedJsonContext>().AllowManyDialects().UseAdoNetMySql(fixture.ConnectionString).Options);

    private async Task CreateTable()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS `UnsignedJsonRows`; CREATE TABLE `UnsignedJsonRows` (`Id` INT PRIMARY KEY, `Payload` JSON NOT NULL, `NullablePayload` JSON NOT NULL);";
        await command.ExecuteNonQueryAsync(Ct);
    }

    public static TheoryData<UnsignedEnumValue?, string> Rows => new()
    {
        { UnsignedEnumValue.Min, "{\"Prop\": 0}" },
        { UnsignedEnumValue.One, "{\"Prop\": 1}" },
        { UnsignedEnumValue.Max, "{\"Prop\": 18446744073709551615}" },
        { null, "{\"Prop\": null}" }
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_ulong_enum_in_an_owned_JSON_document_round_trips_exact(UnsignedEnumValue? value, string expectedJson)
    {
        await CreateTable();
        await using (var context = NewContext())
        {
            context.Rows.Add(new UnsignedJsonRow
            {
                Id = 1,
                Payload = new UnsignedJsonPayload { Prop = value ?? UnsignedEnumValue.Min },
                NullablePayload = new UnsignedNullableJsonPayload { Prop = value }
            });
            await context.SaveChangesAsync(Ct);
        }
        await using var read = NewContext();
        var row = await read.Rows.SingleAsync(r => r.Id == 1, Ct);

        Assert.Equal(value ?? UnsignedEnumValue.Min, row.Payload.Prop);
        Assert.Equal(value, row.NullablePayload.Prop);
        Assert.Contains(expectedJson.Replace(" ", string.Empty), (await ReadJson()).Replace(" ", string.Empty), StringComparison.Ordinal);
    }

    private async Task<string> ReadJson()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONCAT(CAST(`Payload` AS CHAR), CAST(`NullablePayload` AS CHAR)) FROM `UnsignedJsonRows` WHERE `Id` = 1";
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
