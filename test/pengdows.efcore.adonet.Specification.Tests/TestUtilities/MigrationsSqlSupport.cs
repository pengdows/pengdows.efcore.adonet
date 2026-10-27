using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Specification.Tests;

internal static class MigrationsSqlSupport
{
    public static async Task ExecuteAsync(MigrationsInfrastructureFixtureBase fixture, string sql)
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
