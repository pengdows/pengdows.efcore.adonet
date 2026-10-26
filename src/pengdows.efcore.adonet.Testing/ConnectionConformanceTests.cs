using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     The ways an application hands a context its connection, and what the context then owns: a connection string or a
///     connection, in <c>OnConfiguring</c>, in options from dependency injection, or set later; a connection the context
///     owns is disposed with it, one it borrows is left as found.
/// </summary>
/// <remarks>
///     Covers SQL Server's and Npgsql's <c>ConnectionSpecificationTest</c> and Pomelo's named connection string. Each
///     upstream test runs with EF's own internal service provider and with one the application builds; here
///     <paramref name="ownServiceProvider" /> does the same. Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite and
///     DuckDB. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.SqlServer.FunctionalTests/ConnectionSpecificationTest.cs
/// </remarks>
public abstract class ConnectionConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();
        await Fixture.ResetAsync();
        await using var context = Fixture.CreateContext();
        context.Add(new Blog { Name = "connected", CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private IAdoNetDialect Dialect
        => Fixture.ExpectedDialect;

    private DbConnection NewConnection()
    {
        var connection = Fixture.Factory.CreateConnection()!;
        connection.ConnectionString = Fixture.ConnectionString;
        return connection;
    }

    private IServiceProvider? Services(bool ownServiceProvider)
        => ownServiceProvider ? new ServiceCollection().AddEntityFrameworkAdoNet(Dialect).BuildServiceProvider() : null;

    // Drivers show disposal differently: most raise Component.Disposed, some (disposed asynchronously) only refuse to open
    // again, and some (Microsoft.Data.Sqlite, DuckDB.NET) reopen a disposed connection. Either sign counts.
    private static DisposalProbe Probe(DbConnection connection)
        => new(connection);

    private sealed class DisposalProbe
    {
        private readonly DbConnection _connection;
        private bool _disposedEvent;

        public DisposalProbe(DbConnection connection)
        {
            _connection = connection;
            connection.Disposed += (_, _) => _disposedEvent = true;
        }

        public bool IsDisposed
            => _disposedEvent || RefusesToOpen();

        private bool RefusesToOpen()
        {
            try
            {
                _connection.Open();
                _connection.Close();
                return false;
            }
            catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException)
            {
                return true;
            }
        }
    }

    private static async Task<bool> WorksAsync(ConfiguredContext context)
        => await context.Blogs.AnyAsync(b => b.Name == "connected", Ct);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_connection_string_in_OnConfiguring(bool ownServiceProvider)
    {
        await using var context = new ConfiguredContext(b => b.UseAdoNet(Fixture.Factory, Fixture.ConnectionString, Dialect), Services(ownServiceProvider));

        Assert.True(await WorksAsync(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_connection_string_in_OnConfiguring_until_one_is_set(bool ownServiceProvider)
    {
        await using var context = new ConfiguredContext(b => b.UseAdoNet(Fixture.Factory, o => o.UseDialect(Dialect)), Services(ownServiceProvider));
        context.Database.SetConnectionString(Fixture.ConnectionString);

        Assert.True(await WorksAsync(context));
    }

    [Fact]
    public async Task A_context_with_no_connection_and_no_connection_string_says_so()
    {
        await using var context = new ConfiguredContext(b => b.UseAdoNet(Fixture.Factory, o => o.UseDialect(Dialect)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => WorksAsync(context));
    }

    [Fact]
    public async Task A_context_with_no_provider_says_so()
    {
        await using var context = new ConfiguredContext(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => WorksAsync(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_borrowed_connection_in_OnConfiguring_is_left_usable(bool ownServiceProvider)
    {
        await using var connection = NewConnection();
        var probe = Probe(connection);

        await using (var context = new ConfiguredContext(
                         b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(Dialect)), Services(ownServiceProvider)))
        {
            Assert.True(await WorksAsync(context));
            Assert.Same(connection, context.Database.GetDbConnection());
        }

        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.False(probe.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_owned_connection_in_OnConfiguring_is_disposed_with_the_context(bool ownServiceProvider)
    {
        var connection = NewConnection();
        var probe = Probe(connection);

        await using (var context = new ConfiguredContext(
                         b => b.UseAdoNet(connection, contextOwnsConnection: true, o => o.UseDialect(Dialect)), Services(ownServiceProvider)))
        {
            Assert.True(await WorksAsync(context));
        }

        Assert.True(probe.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_connection_set_later_is_disposed_only_when_the_context_owns_it(bool contextOwnsConnection)
    {
        var connection = NewConnection();
        var probe = Probe(connection);

        await using (var context = new ConfiguredContext(b => b.UseAdoNet(Fixture.Factory, o => o.UseDialect(Dialect))))
        {
            context.Database.SetDbConnection(connection, contextOwnsConnection);
            Assert.True(await WorksAsync(context));
        }

        Assert.Equal(contextOwnsConnection, probe.IsDisposed);
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task A_connection_can_be_replaced_by_another()
    {
        await using var connection = NewConnection();
        await using var other = NewConnection();
        await using var context = new ConfiguredContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(Dialect)));
        Assert.True(await WorksAsync(context));

        context.Database.SetDbConnection(other);

        Assert.Same(other, context.Database.GetDbConnection());
        Assert.True(await WorksAsync(context));
    }

    [Fact]
    public async Task An_open_owned_connection_cannot_be_replaced()
    {
        var connection = NewConnection();
        await using var other = NewConnection();
        await using var context = new ConfiguredContext(b => b.UseAdoNet(connection, contextOwnsConnection: true, o => o.UseDialect(Dialect)));
        await context.Database.OpenConnectionAsync(Ct);

        Assert.Throws<InvalidOperationException>(() => context.Database.SetDbConnection(other));

        Assert.Same(connection, context.Database.GetDbConnection());
    }

    [Fact]
    public async Task An_open_borrowed_connection_can_be_replaced()
    {
        await using var connection = NewConnection();
        await using var other = NewConnection();
        await using var context = new ConfiguredContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(Dialect)));
        await context.Database.OpenConnectionAsync(Ct);

        context.Database.SetDbConnection(other);

        Assert.Same(other, context.Database.GetDbConnection());
        Assert.True(await WorksAsync(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_connection_the_application_opened_stays_open_and_one_it_closed_is_closed_again(bool openedByApplication)
    {
        await using var connection = NewConnection();
        if (openedByApplication)
        {
            await connection.OpenAsync(Ct);
        }

        var (opened, closed) = (0, 0);
        connection.StateChange += (_, e) =>
        {
            if (e.CurrentState == ConnectionState.Open)
            {
                opened++;
            }
            else if (e.CurrentState == ConnectionState.Closed)
            {
                closed++;
            }
        };

        await using (var context = new ConfiguredContext(b => b.UseAdoNet(connection, contextOwnsConnection: false, o => o.UseDialect(Dialect))))
        {
            Assert.True(await WorksAsync(context));
        }

        Assert.Equal(openedByApplication ? ConnectionState.Open : ConnectionState.Closed, connection.State);
        Assert.Equal(openedByApplication ? (0, 0) : (1, 1), (opened, closed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Options_from_dependency_injection_reach_OnConfiguring(bool generic)
    {
        var services = new ServiceCollection();
        if (generic)
        {
            services.AddDbContext<ConfiguredContext>(b => b.UseAdoNet(Fixture.Factory, Fixture.ConnectionString, Dialect));
        }
        else
        {
            services.AddScoped(_ => new DbContextOptionsBuilder().UseAdoNet(Fixture.Factory, Fixture.ConnectionString, Dialect).Options);
            services.AddScoped(p => new ConfiguredContext(p.GetRequiredService<DbContextOptions>()));
        }

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConfiguredContext>();

        Assert.True(await WorksAsync(context));
        Assert.True(context.OptionsWereConfigured);
    }

    [Fact]
    public async Task A_named_connection_string_is_read_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Blogs"] = Fixture.ConnectionString })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddDbContext<ConfiguredContext>(b => b.UseAdoNet(Fixture.Factory, "Name=ConnectionStrings:Blogs", Dialect));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConfiguredContext>();

        Assert.True(await WorksAsync(context));
    }

    /// <summary>A context over the blog tables, configured in <c>OnConfiguring</c>, from options, or both.</summary>
    public sealed class ConfiguredContext : DbContext
    {
        private readonly Action<DbContextOptionsBuilder>? _configure;
        private readonly IServiceProvider? _services;

        public ConfiguredContext(Action<DbContextOptionsBuilder> configure, IServiceProvider? services = null)
        {
            _configure = configure;
            _services = services;
        }

        public ConfiguredContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Blog> Blogs
            => Set<Blog>();

        /// <summary>Whether options from dependency injection were already configured when OnConfiguring ran.</summary>
        public bool OptionsWereConfigured { get; private set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            OptionsWereConfigured = optionsBuilder.IsConfigured;
            _configure?.Invoke(optionsBuilder);
            if (_services != null)
            {
                optionsBuilder.UseInternalServiceProvider(_services);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Blog>().Ignore(b => b.Posts);
    }
}
