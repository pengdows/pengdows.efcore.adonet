using System.Data.Common;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.duckdb.Tests;

public sealed class DuckDbFixture : ProviderFixture
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adonet-{Guid.NewGuid():N}.duckdb");

    public override DbProviderFactory Factory => DuckDBClientFactory.Instance;
    public override string ConnectionString => $"Data Source={_path}";
    public override IAdoNetDialect ExpectedDialect => DuckDbDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetDuckDb(ConnectionString);

    protected override IEnumerable<string> Ddl =>
    [
        "CREATE SEQUENCE \"BlogIds\"",
        "CREATE SEQUENCE \"PostIds\"",
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER PRIMARY KEY DEFAULT nextval('BlogIds'),
            "Name" VARCHAR NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" UUID NOT NULL)
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER PRIMARY KEY DEFAULT nextval('PostIds'),
            "BlogId" INTEGER NOT NULL,
            "Title" VARCHAR NOT NULL)
        """
    ];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_path);
        File.Delete(_path + ".wal");
    }
}

public class DuckDbConformanceTests(DuckDbFixture fixture) : ProviderConformanceTests<DuckDbFixture>(fixture);
