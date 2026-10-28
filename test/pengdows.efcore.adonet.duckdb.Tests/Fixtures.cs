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

    // DuckDB refuses ALTER TABLE on a table with indexes or foreign keys ("Cannot alter entry ... because there are entries
    // that depend on it"). A dialect could drop and recreate them around every alteration; this one refuses rather than pretend.
    public override IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>
    {
        ["Migrate_applies_records_and_reverts_migrations"] = DependentAlter,
        ["Migrations_apply_synchronously_too"] = DependentAlter,
        ["Alter_column_changes_size_nullability_and_default"] = DependentAlter,

        // CREATE TABLE can't name a table not yet created, ALTER TABLE can't add a foreign key, and a table another refers
        // to can't be dropped to rebuild it: a cycle of foreign keys can't be declared at all.
        ["Ensure_created_creates_tables_whose_foreign_keys_refer_to_each_other"] = "DuckDB 1.5 can't declare foreign keys that refer to each other",

        // EF deletes the leaf, then its parents, in one transaction; DuckDB still counts the deleted leaf as referencing them
        // ("Violates foreign key constraint ... still referenced", its documented foreign key limitation).
        ["Ensure_created_creates_a_table_reached_by_two_cascade_paths_and_deletes_cascade"] =
            "DuckDB 1.5 checks foreign keys against rows deleted earlier in the same transaction"
    };

    private const string DependentAlter = "DuckDB 1.5 can't ALTER a table that has indexes or foreign keys";

    public override IReadOnlyList<string> ModelSequences { get; } =
        ["SchemaAuthors_Id_seq", "SchemaBooks_Id_seq", "SchemaCategories_Id_seq", "LongNames_Id_seq", "Authors_Id_seq", "Shelves_Id_seq"];

    protected override IEnumerable<string> Ddl =>
    [
        "CREATE SEQUENCE \"BlogIds\"",
        "CREATE SEQUENCE \"PostIds\"",
        "CREATE SEQUENCE \"TallyIds\"",
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
        """,
        "CREATE TABLE \"Tallies\" (\"Id\" INTEGER PRIMARY KEY DEFAULT nextval('TallyIds'))"
    ];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_path);
        File.Delete(_path + ".wal");
    }
}

public class DuckDbConformanceTests(DuckDbFixture fixture) : ProviderConformanceTests<DuckDbFixture>(fixture);

public class DuckDbMigrationsConformanceTests(DuckDbFixture fixture) : MigrationsConformanceTests<DuckDbFixture>(fixture);

public class DuckDbBulkConformanceTests(DuckDbFixture fixture) : BulkConformanceTests<DuckDbFixture>(fixture);

public class DuckDbTranslationConformanceTests(DuckDbFixture fixture) : TranslationConformanceTests<DuckDbFixture>(fixture);

public class DuckDbLiteralConformanceTests(DuckDbFixture fixture) : LiteralConformanceTests<DuckDbFixture>(fixture);

public class DuckDbDatabaseCreatorConformanceTests(DuckDbFixture fixture) : DatabaseCreatorConformanceTests<DuckDbFixture>(fixture);

public class DuckDbConnectionConformanceTests(DuckDbFixture fixture) : ConnectionConformanceTests<DuckDbFixture>(fixture);

public class DuckDbExecutionStrategyConformanceTests(DuckDbFixture fixture) : ExecutionStrategyConformanceTests<DuckDbFixture>(fixture);
