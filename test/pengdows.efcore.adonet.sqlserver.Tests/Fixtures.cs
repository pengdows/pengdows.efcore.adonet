using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

public sealed class SqlServerFixture : ContainerFixture
{
    public override DbProviderFactory Factory => SqlClientFactory.Instance;
    public override IAdoNetDialect ExpectedDialect => SqlServerDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetSqlServer(ConnectionString);

    protected override IDatabaseContainer CreateContainer()
        => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override IEnumerable<string> Ddl =>
    [
        """
        CREATE TABLE "Blogs" (
            "Id" INT IDENTITY PRIMARY KEY,
            "Name" NVARCHAR(MAX) NOT NULL,
            "IsActive" BIT NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" DATETIME2 NOT NULL,
            "ExternalId" UNIQUEIDENTIFIER NOT NULL)
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INT IDENTITY PRIMARY KEY,
            "BlogId" INT NOT NULL,
            "Title" NVARCHAR(MAX) NOT NULL)
        """
    ];
}

public class SqlServerConformanceTests(SqlServerFixture fixture) : ProviderConformanceTests<SqlServerFixture>(fixture);
