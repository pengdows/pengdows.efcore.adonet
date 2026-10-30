using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A JSON property set in a bulk update is jsonb_set with the new value as jsonb: a number or boolean is converted, as jsonb_set
///     takes no integer.
/// </summary>
public class PostgreSqlJsonSetTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Setting_a_json_member_to_a_number_updates_that_member()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var template = PostgreSqlDialect.Instance.Sql.Json?.Set
            ?? throw new InvalidOperationException("PostgreSQL declares a JSON set.");
        var sql = string.Format(CultureInfo.InvariantCulture, template, "'{\"Views\": 1}'::jsonb", "'$.Views'", "27");

        await context.Database.OpenConnectionAsync(ct);
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT (" + sql + ")::text";
        var text = (string?)await command.ExecuteScalarAsync(ct);

        Assert.Equal("{\"Views\": 27}", text);
    }
}
