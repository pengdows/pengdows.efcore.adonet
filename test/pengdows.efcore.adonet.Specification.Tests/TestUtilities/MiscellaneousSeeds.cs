namespace pengdows.efcore.adonet.Specification.Tests;

internal static class MiscellaneousSeeds
{
    public static async Task SeedNullKey<THost>(DbContext context)
        where THost : IDatabaseHost, new()
    {
        var host = new THost();
        var table = host.Dialect.Sql.IdentifierQuotePrefix + "ZeroKey" + host.Dialect.Sql.IdentifierQuoteSuffix;
        var id = host.Dialect.Sql.IdentifierQuotePrefix + "Id" + host.Dialect.Sql.IdentifierQuoteSuffix;
        var type = host.Dialect.TypeMappings.GetStoreType(typeof(int)) ?? "INTEGER";
        await context.Database.ExecuteSqlRawAsync($"CREATE TABLE {table} ({id} {type})");
        await context.Database.ExecuteSqlRawAsync($"INSERT INTO {table} ({id}) VALUES (NULL)");
    }
}
