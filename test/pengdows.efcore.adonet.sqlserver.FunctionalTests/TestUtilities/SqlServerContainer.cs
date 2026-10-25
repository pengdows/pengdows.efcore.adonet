using Testcontainers.MsSql;

namespace Microsoft.EntityFrameworkCore.TestUtilities;

/// <summary>pengdows: one SQL Server 2022 container per test run, used when Test__SqlServer__DefaultConnection is unset.</summary>
public static class SqlServerContainer
{
    private static readonly Lazy<string> Started = new(() =>
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        container.StartAsync().GetAwaiter().GetResult();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return container.GetConnectionString() + ";Connect Timeout=60;ConnectRetryCount=0";
    });

    public static string ConnectionString
        => Started.Value;
}
