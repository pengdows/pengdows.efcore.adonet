using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace pengdows.efcore.adonet.Testing;

/// <summary>A server database started in Docker through Testcontainers. Without Docker the suite is skipped.</summary>
public abstract class ContainerFixture : ProviderFixture
{
    private IDatabaseContainer? _container;
    private string? _connectionString;

    public override string ConnectionString
        => _connectionString ?? throw new InvalidOperationException("The container has not started.");

    protected abstract IDatabaseContainer CreateContainer();

    public override async ValueTask InitializeAsync()
    {
        try
        {
            _container = CreateContainer();
            await _container.StartAsync();
        }
        catch (DockerUnavailableException e)
        {
            SkipReason = $"Docker is not available: {e.Message}";
            return;
        }

        _connectionString = _container.GetConnectionString();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
