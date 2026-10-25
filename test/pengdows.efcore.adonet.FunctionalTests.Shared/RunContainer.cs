using DotNet.Testcontainers.Containers;

namespace pengdows.efcore.adonet.FunctionalTests;

/// <summary>
///     One database container for the whole test run, started on first use and disposed when the process exits (Testcontainers'
///     reaper also removes it if the process dies). EF's test stores create their own databases inside it.
/// </summary>
public sealed class RunContainer(Func<IDatabaseContainer> create, string connectionStringSuffix = "")
{
    private readonly Lazy<string> _connectionString = new(() =>
    {
        var container = create();
        container.StartAsync().GetAwaiter().GetResult();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return container.GetConnectionString() + connectionStringSuffix;
    });

    public string ConnectionString
        => _connectionString.Value;
}
