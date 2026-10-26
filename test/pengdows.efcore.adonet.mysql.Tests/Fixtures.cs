using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Testcontainers.MySql;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

public sealed class MySqlFixture : ContainerFixture
{
    public override DbProviderFactory Factory => MySqlConnectorFactory.Instance;
    public override IAdoNetDialect ExpectedDialect => MySqlDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetMySql(ConnectionString);

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
