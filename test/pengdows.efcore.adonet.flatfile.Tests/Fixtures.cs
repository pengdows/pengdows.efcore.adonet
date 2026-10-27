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

        // Directory mode has no current-schema setting for unqualified history-table lookup.
        ["Another_schemas_history_table_is_not_this_ones"] = "flatfile has no current schema for unqualified history-table lookup",

        // The engine intentionally holds its writer lock for the connection lifetime.
        ["An_open_borrowed_connection_can_be_replaced"] = "flatfile writer lock is held for the connection lifetime",

    };

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

public class FlatFileDatabaseCreatorConformanceTests(FlatFileFixture fixture) : DatabaseCreatorConformanceTests<FlatFileFixture>(fixture);

public class FlatFileConnectionConformanceTests(FlatFileFixture fixture) : ConnectionConformanceTests<FlatFileFixture>(fixture);

public class FlatFileExecutionStrategyConformanceTests(FlatFileFixture fixture) : ExecutionStrategyConformanceTests<FlatFileFixture>(fixture);
