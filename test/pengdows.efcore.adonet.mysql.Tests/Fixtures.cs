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

    protected override IDatabaseContainer CreateContainer()
        => new MySqlBuilder("mysql:8.4").Build();

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
        """
    ];
}

public class MySqlConformanceTests(MySqlFixture fixture) : ProviderConformanceTests<MySqlFixture>(fixture);

public class MySqlMigrationsConformanceTests(MySqlFixture fixture) : MigrationsConformanceTests<MySqlFixture>(fixture);
