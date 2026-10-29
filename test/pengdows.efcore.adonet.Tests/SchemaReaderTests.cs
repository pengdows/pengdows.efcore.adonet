using System.Data.Common;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Scaffolding;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Reverse engineering reads through the dialect's schema reader, which may read any catalog: ISO's INFORMATION_SCHEMA
///     is one reader, not the contract (SQLite's catalog is sqlite_master and PRAGMAs).
/// </summary>
public class SchemaReaderTests
{
    [Fact]
    public void A_dialects_own_catalog_reader_reads_the_model()
    {
        var dialect = new CatalogDialect();
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake" };

        var model = new AdoNetDatabaseModelFactory(dialect).Create(connection, new DatabaseModelFactoryOptions());

        Assert.Equal(["from the catalog"], model.Tables.Select(t => t.Name));
    }

    // EF's tools scaffold from a connection string (dotnet ef dbcontext scaffold): a plugin's design-time services name its
    // driver, and the connection string is opened through that driver's factory.
    [Fact]
    public void A_connection_string_is_read_through_the_plugins_driver()
    {
        var factory = new fakeDbFactory(pengdows.crud.enums.SupportedDatabase.PostgreSql);

        var model = DesignTimeModelFactory(new DriverDesignTimeServices(factory))
            .Create("Data Source=fake;EmulatedProduct=PostgreSql", new DatabaseModelFactoryOptions());

        Assert.Equal(["from the catalog"], model.Tables.Select(t => t.Name));
        var connection = Assert.Single(factory.CreatedConnections);
        Assert.Equal("Data Source=fake;EmulatedProduct=PostgreSql", connection.ConnectionString);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Without_a_driver_a_connection_string_cannot_be_read()
        => Assert.Contains(
            "doesn't name its driver",
            Assert.Throws<NotSupportedException>(
                () => DesignTimeModelFactory(new DriverDesignTimeServices(null)).Create("Data Source=fake", new DatabaseModelFactoryOptions())).Message);

    // A design-time context has no options to choose a dialect: a plugin tells servers apart from the open connection.
    [Fact]
    public void A_plugin_chooses_the_dialect_from_the_open_connection()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake" };

        var model = new ServerChoosingModelFactory().Create(connection, new DatabaseModelFactoryOptions());

        Assert.Equal(["Open"], model.Tables.Select(t => t.Name));
    }

    private sealed class ServerChoosingModelFactory() : AdoNetDatabaseModelFactory(AdoNetDialect.Ansi, null)
    {
        protected override IAdoNetDialect DialectFor(DbConnection connection)
            => new StateDialect(connection.State.ToString());
    }

    private sealed class StateDialect(string table) : AdoNetDialect
    {
        public override string Name => "State";
        public override SchemaReader SchemaReader { get; } = new NamedReader(table);
    }

    private sealed class NamedReader(string table) : SchemaReader
    {
        public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
        {
            var model = new DatabaseModel();
            model.Tables.Add(new DatabaseTable { Database = model, Name = table });
            return model;
        }
    }

    private static IDatabaseModelFactory DesignTimeModelFactory(IDesignTimeServices designTimeServices)
    {
        var services = new ServiceCollection();
        designTimeServices.ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider().GetRequiredService<IDatabaseModelFactory>();
    }

    private sealed class DriverDesignTimeServices(DbProviderFactory? factory)
        : pengdows.efcore.adonet.Design.AdoNetDesignTimeServices(new CatalogDialect(), factory);

    [Fact]
    public void A_catalog_delete_rule_reads_as_efs_referential_action()
        => Assert.Equal(
            [Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade, Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull, null],
            CatalogReader.Actions(["cascade", "SET NULL", "'; DROP TABLE x; --"]));

    private sealed class CatalogDialect : AdoNetDialect
    {
        public override string Name => "Catalog";
        public override SchemaReader SchemaReader { get; } = new CatalogReader();
    }

    private sealed class CatalogReader : SchemaReader
    {
        public override DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql)
        {
            var model = new DatabaseModel();
            model.Tables.Add(new DatabaseTable { Database = model, Name = "from the catalog" });
            return model;
        }

        public static List<Microsoft.EntityFrameworkCore.Migrations.ReferentialAction?> Actions(string[] rules)
            => rules.Select(r => new CatalogReader().DeleteAction(r)).ToList();
    }
}
