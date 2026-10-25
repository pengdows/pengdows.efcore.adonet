using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>An unqualified history table is the one in the current schema, not one of the same name elsewhere.</summary>
public class PostgreSqlHistoryTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Another_schemas_history_table_is_not_this_ones()
    {
        fixture.SkipIfNotRunnable();
        await fixture.ExecuteAsync("CREATE SCHEMA IF NOT EXISTS tenant");
        await fixture.ExecuteAsync("""CREATE TABLE IF NOT EXISTS tenant."__EFMigrationsHistory" ("MigrationId" VARCHAR(150) PRIMARY KEY, "ProductVersion" VARCHAR(32))""");

        await using var context = fixture.CreateContext<MigrationsContext>(o => new(o));

        Assert.False(await context.GetService<IHistoryRepository>().ExistsAsync(TestContext.Current.CancellationToken));
    }
}
