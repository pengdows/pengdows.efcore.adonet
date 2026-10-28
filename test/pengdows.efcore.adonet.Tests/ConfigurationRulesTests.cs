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
        var refused = Assert.Throws<InvalidOperationException>(() => Context(b => b.UseAdoNet(new fakeDbFactory(SupportedDatabase.PostgreSql), ConnectionString)));

        Assert.Contains(nameof(fakeDbFactory), refused.Message);
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
        using var context = Context(b => b.UseAdoNet(new fakeDbFactory(SupportedDatabase.PostgreSql), ConnectionString, o => o.UseDialect(AdoNetDialect.Ansi)));

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
    public void Options_with_no_connection_source_are_refused()
    {
        var builder = new DbContextOptionsBuilder<ItemContext>().AllowManyDialects();
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new pengdows.efcore.adonet.Infrastructure.AdoNetOptionsExtension().WithDialect(AdoNetDialect.Ansi));
        using var context = new ItemContext(builder.Options);

        Assert.Contains("Configure exactly one", Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection()).Message);
    }

    [Fact]
    public void Data_sources_the_provider_creates_are_disposed_with_its_service_provider()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql) { SupportsNativeDataSource = true };
        var services = new ServiceCollection().AddEntityFrameworkAdoNet(AdoNetDialect.Ansi).BuildServiceProvider();

        using (var context = Context(b => b.UseAdoNet(factory, ConnectionString, AdoNetDialect.Ansi).UseInternalServiceProvider(services)))
        {
            context.Database.GetDbConnection();
        }

        Assert.False(factory.CreatedDataSources.Single().WasDisposed);
        services.Dispose();
        Assert.True(factory.CreatedDataSources.Single().WasDisposed);
    }

    [Fact]
    public void A_data_source_with_a_known_dialect_is_not_probed_for_its_provider()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        using var dataSource = factory.CreateDataSource(ConnectionString);

        using (var context = Context(b => b.UseAdoNet(dataSource, AdoNetDialect.Ansi)))
        {
            Assert.Same(AdoNetDialect.Ansi, context.GetService<IAdoNetDialect>());
        }

        using (var context = Context(b => b.UseAdoNet(dataSource, o => o.UseDialect(AdoNetDialect.Ansi))))
        {
            Assert.Same(AdoNetDialect.Ansi, context.GetService<IAdoNetDialect>());
        }

        Assert.Empty(factory.CreatedConnections);
    }

    [Fact]
    public void A_data_source_whose_provider_is_unknown_is_probed_only_once()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        using var dataSource = factory.CreateDataSource(ConnectionString);

        Assert.Throws<InvalidOperationException>(() => Context(b => b.UseAdoNet(dataSource)));

        Assert.Single(factory.CreatedConnections);
    }

    [Fact]
    public void A_data_source_without_a_dialect_is_probed_once_for_a_registered_one()
    {
        AdoNetDialects.Register<ProbedFactory>(TestDialects.Limit);
        var factory = new ProbedFactory();
        using var dataSource = factory.CreateDataSource(ConnectionString);

        using var context = Context(b => b.UseAdoNet(dataSource));

        Assert.Same(TestDialects.Limit, context.GetService<IAdoNetDialect>());
        Assert.Single(factory.CreatedConnections);
    }

    // A fakeDb factory under a type of its own: dialects are registered by factory type, process-wide, so registering one for
    // fakeDbFactory itself would reach every other test. Its connections report it as their factory.
    private sealed class ProbedFactory() : fakeDbFactory(SupportedDatabase.PostgreSql);
}
