using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MySqlConnector;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A real SingleStore server (no Testcontainers.SingleStore package exists, so this builds the container directly):
///     confirms AutoDetect reads its handshake (a MySQL-compatible version, by design) and its @@version_comment (the one
///     signal that actually names it) the way the fakeDb cases assume.
/// </summary>
public sealed class SingleStoreAutoDetectTests : IAsyncLifetime
{
    private const string Password = "SingleStore123!";
    private IContainer? _container;

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder("singlestore/cluster-in-a-box:latest")
            .WithEnvironment("ROOT_PASSWORD", Password)
            .WithEnvironment("START_AFTER_INIT", "Y")
            .WithPortBinding(3306, true)
            .Build();
        await _container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private string ConnectionString
        => new MySqlConnectionStringBuilder
        {
            Server = _container!.Hostname,
            Port = _container.GetMappedPublicPort(3306),
            UserID = "root",
            Password = Password,
            AllowPublicKeyRetrieval = true,
            ConnectionTimeout = 2
        }.ConnectionString;

    // The container reports its port open before the server inside has finished bootstrapping and will accept
    // connections (observed directly: ~30-40 seconds after the port opens); retry instead of trusting the port alone.
    private async Task<MySqlDialect> AutoDetectWithRetryAsync(CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                return await MySqlDialect.AutoDetectAsync(MySqlConnectorFactory.Instance, ConnectionString, cancellationToken);
            }
            catch (Exception exception)
            {
                last = exception;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new InvalidOperationException("SingleStore never became ready to accept connections.", last);
    }

    [Fact]
    public async Task A_real_SingleStore_server_is_detected_as_SingleStore()
        => Assert.Same(MySqlDialect.SingleStore, await AutoDetectWithRetryAsync(TestContext.Current.CancellationToken));
}
