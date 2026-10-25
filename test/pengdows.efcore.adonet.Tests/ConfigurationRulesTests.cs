using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Configuration mistakes fail at configuration time, and the provider owns what it creates.</summary>
public class ConfigurationRulesTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql";

    private static ItemContext Context(Func<DbContextOptionsBuilder<ItemContext>, DbContextOptionsBuilder<ItemContext>> configure)
        => new(configure(new DbContextOptionsBuilder<ItemContext>().AllowManyDialects()).Options);

    [Fact]
    public void A_factory_no_dialect_is_registered_for_needs_one_chosen()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Context(b => b.UseAdoNet(new UnregisteredFactory(), ConnectionString)));

        Assert.Contains(nameof(UnregisteredFactory), refused.Message);
        Assert.Contains("UseDialect", refused.Message);
    }

    [Fact]
    public void A_connection_no_dialect_is_registered_for_needs_one_chosen()
    {
        using var connection = new fakeDbConnection { ConnectionString = ConnectionString };

        Assert.Contains("UseDialect", Assert.Throws<InvalidOperationException>(() => Context(b => b.UseAdoNet(connection))).Message);
    }

    [Fact]
    public void Iso_sql_is_a_dialect_chosen_like_any_other()
    {
        using var context = Context(b => b.UseAdoNet(new UnregisteredFactory(), ConnectionString, o => o.UseDialect(AdoNetDialect.Ansi)));

        Assert.Same(AdoNetDialect.Ansi, context.GetService<IAdoNetDialect>());
    }

    [Fact]
    public void Configuring_a_source_again_replaces_the_previous_one()
    {
        using var connection = new fakeDbConnection { ConnectionString = ConnectionString };
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);

        using (var context = Context(b => b.UseAdoNet(factory, ConnectionString, AdoNetDialect.Ansi).UseAdoNet(connection)))
        {
            Assert.Same(connection, context.Database.GetDbConnection());
        }

        using (var context = Context(b => b.UseAdoNet(connection, adoNetOptionsAction: o => o.UseDialect(AdoNetDialect.Ansi)).UseAdoNet(factory, ConnectionString)))
        {
            Assert.NotSame(connection, context.Database.GetDbConnection());
            Assert.Equal(ConnectionString, context.Database.GetConnectionString());
        }
    }

    [Fact]
    public void A_connection_string_alone_cannot_replace_a_configured_connection()
    {
        using var connection = new fakeDbConnection { ConnectionString = ConnectionString };
        using var context = Context(b => b.UseAdoNet(connection, adoNetOptionsAction: o => o.UseDialect(AdoNetDialect.Ansi)));

        context.Database.SetDbConnection(null);
        context.Database.SetConnectionString(ConnectionString);

        Assert.Contains("DbProviderFactory", Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection()).Message);
    }

    [Fact]
    public void Data_sources_the_provider_creates_are_disposed_with_its_service_provider()
    {
        var factory = new TrackingFactory();
        var services = new ServiceCollection().AddEntityFrameworkAdoNet(AdoNetDialect.Ansi).BuildServiceProvider();

        using (var context = Context(b => b.UseAdoNet(factory, ConnectionString, AdoNetDialect.Ansi).UseInternalServiceProvider(services)))
        {
            context.Database.GetDbConnection();
        }

        Assert.False(factory.Created.Single().IsDisposed);
        services.Dispose();
        Assert.True(factory.Created.Single().IsDisposed);
    }

    private sealed class UnregisteredFactory : DbProviderFactory
    {
        public override DbConnection CreateConnection()
            => new fakeDbConnection();
    }

    private sealed class TrackingFactory : DbProviderFactory
    {
        public List<TrackingDataSource> Created { get; } = [];

        public override DbConnection CreateConnection()
            => new fakeDbConnection();

        public override DbDataSource CreateDataSource(string connectionString)
        {
            var dataSource = new TrackingDataSource(connectionString);
            Created.Add(dataSource);
            return dataSource;
        }
    }

    private sealed class TrackingDataSource(string connectionString) : DbDataSource
    {
        public bool IsDisposed { get; private set; }

        public override string ConnectionString
            => connectionString;

        protected override DbConnection CreateDbConnection()
            => new fakeDbConnection { ConnectionString = connectionString };

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
