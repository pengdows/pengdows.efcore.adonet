using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     MySQL's model options executed through fakeDb: a migration written as Pomelo's are (its annotations) run by EF's
///     migrator, and a model configured with the fluent methods created by EnsureCreated, each recording the DDL sent: table
///     options, character set and collation, a column's SRID (MariaDB's REF_SYSTEM_ID), a key's and an index's prefix
///     lengths, a FULLTEXT parser, the GUID collation, and an ALTER of the table's options. The DDL-shape tests stay beside
///     these (MySqlModelOptionsDdlTests). Provider-only: MySQL's DDL.
/// </summary>
public class MySqlMigrationExecutionTests
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public sealed class ShelfMigrationContext(DbContextOptions<ShelfMigrationContext> options) : DbContext(options);

    [DbContext(typeof(ShelfMigrationContext))]
    [Migration("20240101000000_Shelves")]
    public sealed class ShelvesMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                    name: "Shelves",
                    columns: table => new
                    {
                        Code = table.Column<string>(type: "VARCHAR(300)", nullable: false).Annotation("MySql:CharSet", "latin1"),
                        Token = table.Column<Guid>(type: "CHAR(36)", nullable: false).Annotation("MySql:GuidCollation", "ascii_bin"),
                        Location = table.Column<byte[]>(type: "POINT", nullable: false).Annotation("MySql:SpatialReferenceSystemId", 4326)
                    },
                    constraints: table => table.PrimaryKey("PK_Shelves", x => x.Code).Annotation("MySql:IndexPrefixLength", new[] { 20 }))
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_bin")
                .Annotation("MySql:StoreOptions", "ENGINE=InnoDB,");
            migrationBuilder.CreateIndex("IX_Shelves_Code", "Shelves", "Code")
                .Annotation("MySql:FullTextIndex", true)
                .Annotation("MySql:FullTextParser", "ngram");
            migrationBuilder.AlterTable("Shelves")
                .Annotation("MySql:StoreOptions", "ENGINE=MyISAM,ROW_FORMAT=COMPACT,")
                .OldAnnotation("MySql:StoreOptions", "ENGINE=InnoDB,");
        }
    }

    [Theory]
    [InlineData(false, "SRID 4326")]
    [InlineData(true, "REF_SYSTEM_ID=4326")]
    public async Task A_migration_with_pomelos_annotations_runs_its_ddl(bool mariaDb, string srid)
    {
        var db = new FakeMySql();
        await using var context = db.Context<ShelfMigrationContext>(o => new ShelfMigrationContext(o), mariaDb);

        await context.Database.MigrateAsync(Ct);

        var ddl = string.Join("\n", db.Commands);
        Assert.Contains("CREATE TABLE \"Shelves\" (", ddl);
        Assert.Contains("\"Code\" VARCHAR(300) CHARACTER SET latin1 NOT NULL", ddl);
        Assert.Contains("\"Token\" CHAR(36) NOT NULL COLLATE \"ascii_bin\"", ddl);
        Assert.Contains("\"Location\" POINT NOT NULL " + srid, ddl);
        Assert.Contains("PRIMARY KEY (\"Code\"(20))", ddl);
        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin ENGINE=InnoDB;", ddl);
        Assert.Contains("CREATE FULLTEXT INDEX \"IX_Shelves_Code\" ON \"Shelves\" (\"Code\") WITH PARSER \"ngram\";", ddl);
        Assert.Contains("ALTER TABLE \"Shelves\" ENGINE=MyISAM ROW_FORMAT=COMPACT;", ddl);
        Assert.Contains("20240101000000_Shelves", string.Join("\n", db.Parameters.Concat(db.Commands)));
    }

    public sealed class Place
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Guid Token { get; set; }
        public global::NetTopologySuite.Geometries.Point Location { get; set; } = null!;
    }

    public sealed class Tag
    {
        public string Code { get; set; } = "";
    }

    public sealed class PlaceContext(DbContextOptions<PlaceContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseGuidCollation("ascii_bin");
            modelBuilder.Entity<Place>(b =>
            {
                b.HasCharSet("utf8mb4");
                b.UseCollation("utf8mb4_bin");
                b.HasTableOption("ENGINE", "InnoDB");
                b.Property(p => p.Name).HasMaxLength(200);
                b.Property(p => p.Location).HasSpatialReferenceSystem(4326);
                b.HasIndex(p => p.Name).IsFullText(true, "ngram");
                b.HasIndex(p => p.Location).IsSpatial();
            });
            modelBuilder.Entity<Tag>(b =>
            {
                b.Property(t => t.Code).HasMaxLength(500);
                b.HasKey(t => t.Code).HasPrefixLength(20);
            });
        }
    }

    // EnsureCreated opens the database, counts its tables (scripted: none) and runs the model's DDL.
    [Theory]
    [InlineData(false, "SRID 4326")]
    [InlineData(true, "REF_SYSTEM_ID=4326")]
    public async Task A_model_configured_with_the_fluent_methods_is_created(bool mariaDb, string srid)
    {
        var db = new FakeMySql();
        await using var context = db.Context<PlaceContext>(o => new PlaceContext(o), mariaDb, o => o.UseNetTopologySuite());
        db.Connection.EnqueueScalarResult(0L);

        Assert.True(await context.Database.EnsureCreatedAsync(Ct));

        var ddl = string.Join("\n", db.Commands);
        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin ENGINE=InnoDB;", ddl);
        Assert.Contains("\"Token\" CHAR(36) NOT NULL COLLATE \"ascii_bin\"", ddl);
        Assert.Contains("\"Location\" POINT NOT NULL " + srid, ddl);
        Assert.Contains("PRIMARY KEY (\"Code\"(20))", ddl);
        Assert.Contains("CREATE FULLTEXT INDEX \"IX_Place_Name\" ON \"Place\" (\"Name\") WITH PARSER \"ngram\";", ddl);
        Assert.Contains("CREATE SPATIAL INDEX \"IX_Place_Location\" ON \"Place\" (\"Location\");", ddl);
    }
}
