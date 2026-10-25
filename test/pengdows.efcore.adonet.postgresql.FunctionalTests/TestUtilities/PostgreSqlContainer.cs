using Testcontainers.PostgreSql;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

/// <summary>pengdows: one PostgreSQL container per test run, used when Test__Npgsql__DefaultConnection is unset.</summary>
public static class PostgreSqlContainer
{
    private static readonly RunContainer Container = new(() => new PostgreSqlBuilder("postgres:17").Build());

    public static string ConnectionString
        => Container.ConnectionString;
}
