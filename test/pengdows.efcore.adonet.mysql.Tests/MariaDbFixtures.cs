using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MariaDb;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     The MariaDB variant of the MySQL dialect against MariaDB, through MySqlConnector, which supports both servers. MariaDB is its own engine
///     since 10.0: where its behaviour differs from MySQL's, the difference is recorded here as a capability skip or met in
///     the dialect. 11.4 is the current long-term-support release.
/// </summary>
public sealed class MariaDbFixture : MySqlFixture
{
    public override IAdoNetDialect ExpectedDialect => MySqlDialect.MariaDb;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetMySql(ConnectionString, o => o.UseDialect(MySqlDialect.MariaDb));

    protected override IDatabaseContainer CreateContainer()
        // As root: the database-creator tests create and drop databases, which the container's own user may not.
        => new MariaDbBuilder("mariadb:11.4").WithUsername("root").Build();
}

public class MariaDbConformanceTests(MariaDbFixture fixture) : ProviderConformanceTests<MariaDbFixture>(fixture);

public class MariaDbMigrationsConformanceTests(MariaDbFixture fixture) : MigrationsConformanceTests<MariaDbFixture>(fixture);

public class MariaDbBulkConformanceTests(MariaDbFixture fixture) : BulkConformanceTests<MariaDbFixture>(fixture);

public class MariaDbTranslationConformanceTests(MariaDbFixture fixture) : TranslationConformanceTests<MariaDbFixture>(fixture);

public class MariaDbLiteralConformanceTests(MariaDbFixture fixture) : LiteralConformanceTests<MariaDbFixture>(fixture);

public class MariaDbDatabaseCreatorConformanceTests(MariaDbFixture fixture) : DatabaseCreatorConformanceTests<MariaDbFixture>(fixture);

public class MariaDbConnectionConformanceTests(MariaDbFixture fixture) : ConnectionConformanceTests<MariaDbFixture>(fixture);

public class MariaDbExecutionStrategyConformanceTests(MariaDbFixture fixture) : ExecutionStrategyConformanceTests<MariaDbFixture>(fixture);

/// <summary>Guards the fixture: the suites above must run against MariaDB, not a MySQL image.</summary>
public class MariaDbServerTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture>
{
    [Fact]
    public async Task The_server_is_mariadb()
        => Assert.Contains(
            "MariaDB",
            Convert.ToString(await fixture.ExecuteScalarAsync("SELECT VERSION()"), System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
}
