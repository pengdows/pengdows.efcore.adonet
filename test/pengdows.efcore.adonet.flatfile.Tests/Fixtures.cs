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
        // Strict ISO SQL, which pengdows.flatfile implements, has no UPDATE ... FROM: an UPDATE whose rows come from a join.
        ["Execute_update_filters_through_a_navigation"] = "strict ISO SQL has no UPDATE ... FROM",

        // ALTER TABLE "Authors" RENAME TO "Writers", then an insert into Shelves: its foreign key still names "Authors".
        ["Migrate_applies_records_and_reverts_migrations"] =
            "pengdows.flatfile 0.2.1-preview.9 leaves foreign keys naming a table after the table is renamed",
        ["Alter_column_changes_size_nullability_and_default"] =
            "pengdows.flatfile 0.2.1-preview.9 leaves foreign keys naming a table after the table is renamed",

        ["Math_on_doubles"] = "pengdows.flatfile 0.2.1-preview.9 has no POWER",

        // DELETE FROM "Blogs" AS "b" ... (ISO SQL:2011 allows a correlation name on a DELETE target): each test deletes
        // through ExecuteDelete, in its body or to clear its table.
        ["Execute_delete_removes_matching_rows"] = DeleteAlias,
        ["Execute_delete_filters_through_a_navigation"] = DeleteAlias,
        ["Synchronous_insert_query_update_delete_and_transaction"] = DeleteAlias,
        ["Constants_match_the_values_parameters_stored"] = DeleteAlias,
        ["Date_offset_constants_and_parts"] = DeleteAlias,

        // INTERVAL '0 01:02:03.5' DAY TO SECOND (ISO). The engine's interval literals have one field and no precision.
        ["Time_span_constants"] = "pengdows.flatfile 0.2.1-preview.9 has no DAY TO SECOND interval literal",

        // WHERE "Double" = 4.9406564584124654E-324: an approximate numeric literal (ISO), read as a decimal, which overflows.
        ["Floating_point_constants_at_the_edges_of_their_types"] =
            "pengdows.flatfile 0.2.1-preview.9 parses a literal with an exponent as a decimal and overflows",

        // SELECT "Day" from a DATE column: GetDateTime and GetFieldValue<DateTime> throw InvalidCastException (a DateOnly).
        ["Bulk_insert_writes_values_as_the_column_type"] = "pengdows.flatfile 0.2.1-preview.9 can't read a DATE as a DateTime",

        // After CREATE TABLE "tenant"."H", an unqualified "H" means that table: CREATE TABLE "H" says it already exists.
        ["Another_schemas_history_table_is_not_this_ones"] =
            "pengdows.flatfile 0.2.1-preview.9 resolves an unqualified table name to a table in any schema"
    };

    private const string DeleteAlias = "pengdows.flatfile 0.2.1-preview.9 rejects an alias on a DELETE target";

    public override IReadOnlyList<string> ModelSequences { get; } =
        ["SchemaAuthors_Id_seq", "SchemaBooks_Id_seq", "SchemaCategories_Id_seq", "LongNames_Id_seq", "Authors_Id_seq", "Writers_Id_seq", "Shelves_Id_seq"];

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

public class FlatFileLiteralConformanceTests(FlatFileFixture fixture) : LiteralConformanceTests<FlatFileFixture>(fixture);
