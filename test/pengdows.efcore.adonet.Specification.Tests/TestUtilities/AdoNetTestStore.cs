namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     A database for one test store, on any of the six databases: made empty, its schema created by the provider from the
///     model, then seeded by EF. Nothing in it names a database.
/// </summary>
public class AdoNetTestStore<THost> : RelationalTestStore
    where THost : IDatabaseHost, new()
{
    private static readonly THost Host = new();

    // EF keys shared stores by store type name and store name, which is the same for every host; one index per host keeps
    // each database's stores apart.
    private static readonly TestStoreIndex Index = new();

    public AdoNetTestStore(string name, bool shared)
        : base(name, shared, Host.CreateConnection(Host.ConnectionStringFor(DatabaseName(name))))
    {
    }

    // Test store names are EF's (e.g. "Northwind"); one database per name and host.
    private static string DatabaseName(string name)
        => "ef_" + name.ToLowerInvariant();

    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => Index;

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => Host.UseProvider(
            builder,
            ConnectionString,
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery).CommandTimeout(300));

    protected override async Task InitializeAsync(Func<DbContext> createContext, Func<DbContext, Task>? seed, Func<DbContext, Task>? clean)
    {
        await Host.CreateEmptyDatabaseAsync(DatabaseName(Name));
        await base.InitializeAsync(createContext, seed, clean);
    }

    // EF's relational store leaves its own connection open after setup. Closed here, as every context's is between uses: a
    // database with one writer at a time (pengdows.flatfile) would otherwise be locked against the tests themselves.
    public override async Task<TestStore> InitializeAsync(
        IServiceProvider? serviceProvider,
        Func<DbContext>? createContext,
        Func<DbContext, Task>? seed = null,
        Func<DbContext, Task>? clean = null)
    {
        await base.InitializeAsync(serviceProvider, createContext, seed, clean);
        CloseConnection();
        return this;
    }

    // The database starts empty: the schema is the model's, created by the provider.
    public override async Task CleanAsync(DbContext context)
        => await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
}

public class AdoNetTestStoreFactory<THost> : RelationalTestStoreFactory
    where THost : IDatabaseHost, new()
{
    public static AdoNetTestStoreFactory<THost> Instance { get; } = new();

    public override TestStore Create(string storeName)
        => new AdoNetTestStore<THost>(storeName, shared: false);

    public override TestStore GetOrCreate(string storeName)
        => new AdoNetTestStore<THost>(storeName, shared: true);

    public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkAdoNet(new THost().Dialect);
}
