using pengdows.efcore.adonet.Infrastructure;

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

    private readonly string _databaseName;

    // A shared store is one database per name. A store of one test's own (EF's ad-hoc suites) is a database of its own,
    // dropped when the test is done: suites deriving from one EF base share its store name, and on a server they run at
    // once, each dropping and recreating the other's database under its connections.
    public AdoNetTestStore(string name, bool shared)
        : this(name, shared, shared ? DatabaseName(name) : OwnDatabaseName(name))
    {
    }

    private AdoNetTestStore(string name, bool shared, string databaseName)
        : base(name, shared, Host.CreateConnection(Host.ConnectionStringFor(databaseName)))
        => _databaseName = databaseName;

    // Test store names are EF's (e.g. "Northwind"); one database per name and host.
    private static string DatabaseName(string name)
        => "ef_" + name.ToLowerInvariant();

    // The store's name and a suffix of its own, within every server's identifier limit (PostgreSQL's is 63 bytes).
    private static string OwnDatabaseName(string name)
    {
        var database = DatabaseName(name);
        return (database.Length > 48 ? database[..48] : database) + "_" + Guid.NewGuid().ToString("N")[..8];
    }

    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => Index;

    // As EF's own test stores: the store's connection, which every context shares (so a nested context can join the outer
    // one's transaction), unless a test asks for a connection string.
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
    {
        var options = SqlLog.AddTo(
            UseConnectionString
                ? Host.UseProvider(builder, ConnectionString, Configure)
                : Host.UseProvider(builder, Connection, Configure));

        return options;
    }

    private static void Configure(AdoNetDbContextOptionsBuilder options)
        => TestServices.Use(Host, options.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery).CommandTimeout(300));

    // As EF's own stores: an empty database, then EnsureCreated, which creates the tables and runs the model's seeding
    // (UseSeeding/UseAsyncSeeding), then the test's own seed. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/TestUtilities/SqliteTestStore.cs
    protected override async Task InitializeAsync(Func<DbContext> createContext, Func<DbContext, Task>? seed, Func<DbContext, Task>? clean)
    {
        // A store initialized again (an ad-hoc test's second model) still has its connection open from the first time: closed
        // first, as CleanAsync does, or dropping the database ends it under the store (PostgreSQL's DROP ... WITH (FORCE)).
        CloseIfOpen();
        await Host.CreateEmptyDatabaseAsync(_databaseName);
        await using var context = createContext();
        if (clean != null)
        {
            await clean(context);
        }

        await context.Database.EnsureCreatedAsync();
        if (seed != null)
        {
            await seed(context);
        }

        // Northwind is shared by fixtures of several suites, whichever creates it first; its procedures come with it.
        if (Name == "Northwind")
        {
            await NorthwindProcedureSeeding.CreateAsync<THost>(context);
        }
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
        if (!Host.KeepStoreConnectionOpen)
        {
            CloseIfOpen();
        }
        return this;
    }

    // EF's raw-SQL tests write [name]; each database quotes it its own way.
    protected override string OpenDelimiter
        => Host.Dialect.Sql.IdentifierQuotePrefix;

    protected override string CloseDelimiter
        => Host.Dialect.Sql.IdentifierQuoteSuffix;

    // Only an open connection: DuckDB.NET throws on closing a closed one (UPSTREAM_ISSUES).
    private void CloseIfOpen()
    {
        if (ConnectionState != System.Data.ConnectionState.Closed)
        {
            CloseConnection();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!Shared)
        {
            await Host.DropDatabaseAsync(_databaseName);
        }
    }

    // The database starts empty, as when a test reseeds it: the schema is the model's, created by the provider.
    public override async Task CleanAsync(DbContext context)
    {
        // The store's connection, which the context shares, may be open: a test opened it.
        CloseIfOpen();
        await Host.CreateEmptyDatabaseAsync(_databaseName);
        // EF's database cleaner drops everything and creates the tables again: an empty schema, ready to seed.
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
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
        => TestServices.Add(new THost(), serviceCollection.AddEntityFrameworkAdoNet(new THost().Dialect));
}
