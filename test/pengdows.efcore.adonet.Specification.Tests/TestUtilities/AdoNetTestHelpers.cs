namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>EF's test helpers for a database: its services, its options, and its logging definitions.</summary>
public class AdoNetTestHelpers<THost> : RelationalTestHelpers
    where THost : IDatabaseHost, new()
{
    private static readonly THost Host = new();

    protected AdoNetTestHelpers()
    {
    }

    public static AdoNetTestHelpers<THost> Instance { get; } = new();

    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => SpatialTypes.Add(Host, services.AddEntityFrameworkAdoNet(Host.Dialect));

    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => Host.UseProvider(optionsBuilder, Host.ConnectionStringFor("ef_helpers"), o => SpatialTypes.Use(Host, o));

    public override LoggingDefinitions LoggingDefinitions { get; } = ProviderServices.LoggingDefinitions(Host.Dialect);
}
