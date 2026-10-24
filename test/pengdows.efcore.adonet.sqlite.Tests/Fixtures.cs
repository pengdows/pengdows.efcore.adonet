using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlite.Tests;

public class SqliteFixture : ProviderFixture
{
    // A named shared-cache in-memory database lives as long as one connection to it is open.
    private readonly SqliteConnection _keepAlive;

    public SqliteFixture()
    {
        ConnectionString = $"Data Source=adonet-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public override DbProviderFactory Factory => SqliteFactory.Instance;
    public override string ConnectionString { get; }
    public override IAdoNetDialect ExpectedDialect => SqliteDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetSqlite(ConnectionString);

    protected override IEnumerable<string> Ddl =>
    [
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" BLOB NOT NULL)
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER PRIMARY KEY,
            "BlogId" INTEGER NOT NULL,
            "Title" TEXT NOT NULL)
        """
    ];

    public override async ValueTask DisposeAsync()
    {
        await _keepAlive.DisposeAsync();
        await base.DisposeAsync();
    }
}

public sealed class LegacySqliteFixture : SqliteFixture
{
    public override IAdoNetDialect ExpectedDialect => LegacySqliteDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetSqlite(ConnectionString, o => o.UseDialect(LegacySqliteDialect.Instance));
}

public class SqliteConformanceTests(SqliteFixture fixture) : ProviderConformanceTests<SqliteFixture>(fixture);

public class LegacySqliteConformanceTests(LegacySqliteFixture fixture) : ProviderConformanceTests<LegacySqliteFixture>(fixture);
