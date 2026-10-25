using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.flatfile;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.flatfile.Tests;

public sealed class FlatFileFixture : ProviderFixture
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adonet-{Guid.NewGuid():N}");

    public FlatFileFixture()
        => Directory.CreateDirectory(_path);

    public override DbProviderFactory Factory => FlatFileProviderFactory.Instance;
    public override string ConnectionString => $"path={_path}";
    public override IAdoNetDialect ExpectedDialect => FlatFileDialect.Instance;

    protected override void UseProvider(DbContextOptionsBuilder builder)
        => builder.UseAdoNetFlatFile(ConnectionString);

    public override IReadOnlyDictionary<string, string> KnownEngineGaps { get; } = new Dictionary<string, string>
    {
        // EF pages the principal in a derived table: FROM (SELECT ... FETCH FIRST 2 ROWS ONLY) AS "b0". Valid SQL:2008.
        ["Query_includes_collection_navigation"] = "pengdows.flatfile 0.2.1-preview.8 rejects FETCH FIRST inside a derived table",

        // UPDATE "Blogs" SET "IsActive" = NOT ("Blogs"."IsActive") ... Valid ISO: an update source is any value expression.
        ["Execute_update_changes_matching_rows"] = "pengdows.flatfile 0.2.1-preview.8 rejects a boolean expression (NOT ...) as an UPDATE value",

        // DELETE FROM "Posts" WHERE "Posts"."Id" IN (SELECT "p0"."Id" FROM "Posts" AS "p0" INNER JOIN "Blogs" AS "b" ON ...
        // WHERE NOT ("b"."IsActive")). Valid ISO.
        ["Execute_delete_filters_through_a_navigation"] =
            "pengdows.flatfile 0.2.1-preview.8 resolves \"b\".\"IsActive\" against Posts inside a joined IN subquery",

        // Not the engine: without a target alias, strict ISO SQL has no form for an UPDATE whose rows come from a join.
        ["Execute_update_filters_through_a_navigation"] = "DmlSyntax.UnaliasedTarget cannot write an UPDATE through a join",

        // Renaming a table renames its key sequence: ALTER SEQUENCE "Authors_Id_seq" RENAME TO "Writers_Id_seq".
        ["Migrate_applies_records_and_reverts_migrations"] = "pengdows.flatfile 0.2.1-preview.8 has no ALTER SEQUENCE ... RENAME TO",
        ["Migrations_apply_synchronously_too"] = "pengdows.flatfile 0.2.1-preview.8 has no ALTER SEQUENCE ... RENAME TO",
        ["Alter_column_changes_size_nullability_and_default"] = "pengdows.flatfile 0.2.1-preview.8 has no ALTER COLUMN (nor ALTER SEQUENCE)",

        // Probed directly: '  Ab%_c\d  ' LIKE '  Ab\%%' ESCAPE '\' is false; LIKE without ESCAPE works.
        ["Constant_patterns_match_wildcards_literally"] = "pengdows.flatfile 0.2.1-preview.8 ignores LIKE ... ESCAPE: an escaped pattern never matches",
        ["Trimming"] = "pengdows.flatfile 0.2.1-preview.8 trims both ends for TRIM(LEADING FROM x) and TRIM(TRAILING FROM x)",
        ["Replace"] = "pengdows.flatfile 0.2.1-preview.8 has no REPLACE",
        ["Math_on_decimals"] = "pengdows.flatfile 0.2.1-preview.8 has no FLOOR, CEILING or ROUND",
        ["Math_on_doubles"] = "pengdows.flatfile 0.2.1-preview.8 has no FLOOR, CEILING or ROUND"
    };

    public override IReadOnlyList<string> ModelSequences { get; } =
        ["SchemaAuthors_Id_seq", "SchemaBooks_Id_seq", "Authors_Id_seq", "Writers_Id_seq", "Shelves_Id_seq"];

    // No identity columns: keys come from the "{table}_{column}_seq" sequences the dialect expects.
    protected override IEnumerable<string> Ddl =>
    [
        "CREATE SEQUENCE \"Blogs_Id_seq\"",
        "CREATE SEQUENCE \"Posts_Id_seq\"",
        "CREATE SEQUENCE \"Tallies_Id_seq\"",
        """
        CREATE TABLE "Blogs" (
            "Id" INTEGER NOT NULL,
            "Name" VARCHAR NOT NULL,
            "IsActive" BOOLEAN NOT NULL,
            "Rating" DECIMAL(18,2) NOT NULL,
            "CreatedAt" TIMESTAMP NOT NULL,
            "ExternalId" UUID NOT NULL,
            CONSTRAINT "PK_Blogs" PRIMARY KEY ("Id"))
        """,
        """
        CREATE TABLE "Posts" (
            "Id" INTEGER NOT NULL,
            "BlogId" INTEGER NOT NULL,
            "Title" VARCHAR NOT NULL,
            CONSTRAINT "PK_Posts" PRIMARY KEY ("Id"))
        """,
        "CREATE TABLE \"Tallies\" (\"Id\" INTEGER NOT NULL, CONSTRAINT \"PK_Tallies\" PRIMARY KEY (\"Id\"))"
    ];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(_path, recursive: true);
    }
}

/// <summary>A server database started in Docker through Testcontainers. Without Docker the suite is skipped.</summary>

public class FlatFileConformanceTests(FlatFileFixture fixture) : ProviderConformanceTests<FlatFileFixture>(fixture);

public class FlatFileMigrationsConformanceTests(FlatFileFixture fixture) : MigrationsConformanceTests<FlatFileFixture>(fixture);

public class FlatFileBulkConformanceTests(FlatFileFixture fixture) : BulkConformanceTests<FlatFileFixture>(fixture);

public class FlatFileTranslationConformanceTests(FlatFileFixture fixture) : TranslationConformanceTests<FlatFileFixture>(fixture);
