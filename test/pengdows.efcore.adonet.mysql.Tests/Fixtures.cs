using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Testcontainers.MySql;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

public class MySqlFixture : ContainerFixture
{
    public override DbProviderFactory Factory => MySqlConnectorFactory.Instance;
    public override IAdoNetDialect ExpectedDialect => MySqlDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetMySql(ConnectionString);

    // A connection the application opens itself must already allow the user variables the dialect reads procedure outputs
    // through; the provider adds the option only to connection strings and closed connections.
    public override string ConnectionString
        => base.ConnectionString + ";Allow User Variables=true;UseAffectedRows=false";

    // MySQL's FLOAT stops at 3.402823466E+38, just below float.MaxValue (3.4028234663852886E+38).
    public override float LargestStorableFloat
        => MathF.BitDecrement(float.MaxValue);

    protected override IDatabaseContainer CreateContainer()
        // As root: the database-creator tests create and drop databases, which the container's own user may not.
        => new MySqlBuilder("mysql:8.4").WithUsername("root").Build();

    protected override IEnumerable<string> Ddl =>
    [
        """
        CREATE TABLE `Blogs` (
            `Id` INT AUTO_INCREMENT PRIMARY KEY,
            `Name` LONGTEXT NOT NULL,
            `IsActive` TINYINT(1) NOT NULL,
            `Rating` DECIMAL(18,2) NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL,
            `ExternalId` CHAR(36) NOT NULL)
        """,
        """
        CREATE TABLE `Posts` (
            `Id` INT AUTO_INCREMENT PRIMARY KEY,
            `BlogId` INT NOT NULL,
            `Title` LONGTEXT NOT NULL)
        """,
        "CREATE TABLE `Tallies` (`Id` INT AUTO_INCREMENT PRIMARY KEY)"
    ];
}

public class MySqlConformanceTests(MySqlFixture fixture) : ProviderConformanceTests<MySqlFixture>(fixture);

public class MySqlMigrationsConformanceTests(MySqlFixture fixture) : MigrationsConformanceTests<MySqlFixture>(fixture);

public class MySqlBulkConformanceTests(MySqlFixture fixture) : BulkConformanceTests<MySqlFixture>(fixture);

public class MySqlTranslationConformanceTests(MySqlFixture fixture) : TranslationConformanceTests<MySqlFixture>(fixture);

public class MySqlLiteralConformanceTests(MySqlFixture fixture) : LiteralConformanceTests<MySqlFixture>(fixture);

public class MySqlDatabaseCreatorConformanceTests(MySqlFixture fixture) : DatabaseCreatorConformanceTests<MySqlFixture>(fixture);

public class MySqlConnectionConformanceTests(MySqlFixture fixture) : ConnectionConformanceTests<MySqlFixture>(fixture);

public class MySqlExecutionStrategyConformanceTests(MySqlFixture fixture) : ExecutionStrategyConformanceTests<MySqlFixture>(fixture);

/// <summary>
///     The same MySQL dialect through Oracle's MySql.Data instead of MySqlConnector: the core speaks only ADO.NET and the
///     dialect only SQL, so the driver is the application's choice. Test-only: MySql.Data is GPL and never shipped.
/// </summary>
public sealed class MySqlDataFixture : MySqlFixture
{
    public override DbProviderFactory Factory => MySql.Data.MySqlClient.MySqlClientFactory.Instance;

    // MySql.Data refuses MySQL 8's caching_sha2_password key exchange over plain TCP unless asked.
    public override string ConnectionString
        => base.ConnectionString + ";AllowPublicKeyRetrieval=true";

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNet(Factory, ConnectionString, MySqlDialect.Instance);
}

public class MySqlDataConformanceTests(MySqlDataFixture fixture) : ProviderConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataMigrationsConformanceTests(MySqlDataFixture fixture) : MigrationsConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataBulkConformanceTests(MySqlDataFixture fixture) : BulkConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataTranslationConformanceTests(MySqlDataFixture fixture) : TranslationConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataLiteralConformanceTests(MySqlDataFixture fixture) : LiteralConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataDatabaseCreatorConformanceTests(MySqlDataFixture fixture) : DatabaseCreatorConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataConnectionConformanceTests(MySqlDataFixture fixture) : ConnectionConformanceTests<MySqlDataFixture>(fixture);

public class MySqlDataExecutionStrategyConformanceTests(MySqlDataFixture fixture) : ExecutionStrategyConformanceTests<MySqlDataFixture>(fixture);
