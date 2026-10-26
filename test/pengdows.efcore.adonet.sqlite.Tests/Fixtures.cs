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

    public override IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>
    {
        // EF's own SQLite provider rebuilds the table instead; this dialect refuses rather than pretend.
        ["Alter_column_changes_size_nullability_and_default"] = "SQLite has no ALTER COLUMN",

        // A NUMERIC column keeps an integer past 2^63 as REAL: 123456789012345678901 reads back as 123456789012345680000.
        ["Wide_decimals_and_ansi_strings_round_trip"] = "SQLite has no exact decimal type past 64-bit integers"
    };

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
        """,
        "CREATE TABLE \"Tallies\" (\"Id\" INTEGER PRIMARY KEY)"
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

public class SqliteMigrationsConformanceTests(SqliteFixture fixture) : MigrationsConformanceTests<SqliteFixture>(fixture);

public class LegacySqliteMigrationsConformanceTests(LegacySqliteFixture fixture) : MigrationsConformanceTests<LegacySqliteFixture>(fixture);

public class SqliteBulkConformanceTests(SqliteFixture fixture) : BulkConformanceTests<SqliteFixture>(fixture);

public class SqliteTranslationConformanceTests(SqliteFixture fixture) : TranslationConformanceTests<SqliteFixture>(fixture);

public class LegacySqliteBulkConformanceTests(LegacySqliteFixture fixture) : BulkConformanceTests<LegacySqliteFixture>(fixture);

public class LegacySqliteTranslationConformanceTests(LegacySqliteFixture fixture) : TranslationConformanceTests<LegacySqliteFixture>(fixture);

public class SqliteLiteralConformanceTests(SqliteFixture fixture) : LiteralConformanceTests<SqliteFixture>(fixture);

public class LegacySqliteLiteralConformanceTests(LegacySqliteFixture fixture) : LiteralConformanceTests<LegacySqliteFixture>(fixture);

public class SqliteDatabaseCreatorConformanceTests(SqliteFixture fixture) : DatabaseCreatorConformanceTests<SqliteFixture>(fixture);

public class SqliteConnectionConformanceTests(SqliteFixture fixture) : ConnectionConformanceTests<SqliteFixture>(fixture);
