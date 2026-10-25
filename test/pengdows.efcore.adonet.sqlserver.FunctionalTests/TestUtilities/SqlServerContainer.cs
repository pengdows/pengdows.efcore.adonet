using Testcontainers.MsSql;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

/// <summary>pengdows: one SQL Server 2022 container per test run, used when Test__SqlServer__DefaultConnection is unset.</summary>
public static class SqlServerContainer
{
    private static readonly RunContainer Container = new(
        () => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(), ";Connect Timeout=60;ConnectRetryCount=0");

    public static string ConnectionString
        => Container.ConnectionString;
}
