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
        db.Connection.EnqueueScalarResult(1L);
        await using var context = db.Context<ShelfMigrationContext>(o => new ShelfMigrationContext(o), mariaDb);

        await context.Database.MigrateAsync(Ct);

        var ddl = string.Join("\n", db.Commands);
        Assert.Contains($"CREATE TABLE {Q}Shelves{Q} (", ddl);
        Assert.Contains($"{Q}Code{Q} VARCHAR(300) CHARACTER SET latin1 NOT NULL", ddl);
        Assert.Contains($"{Q}Token{Q} CHAR(36) NOT NULL COLLATE {Q}ascii_bin{Q}", ddl);
        Assert.Contains($"{Q}Location{Q} POINT NOT NULL " + srid, ddl);
        Assert.Contains($"PRIMARY KEY ({Q}Code{Q}(20))", ddl);
        Assert.Contains(") DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin ENGINE=InnoDB;", ddl);
        Assert.Contains($"CREATE FULLTEXT INDEX {Q}IX_Shelves_Code{Q} ON {Q}Shelves{Q} ({Q}Code{Q}) WITH PARSER {Q}ngram{Q};", ddl);
        Assert.Contains($"ALTER TABLE {Q}Shelves{Q} ENGINE=MyISAM ROW_FORMAT=COMPACT;", ddl);
        Assert.Contains("20240101000000_Shelves", string.Join("\n", db.Parameters.Concat(db.Commands)));
    }

    // Pomelo's migrations test expects the database to hold exactly the migrations' tables, so the migrations lock leaves none
    // behind: MySQL's GET_LOCK is a session lock and creates nothing. Behaviour reference (not copied):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/test/EFCore.MySql.IntegrationTests/Commands/TestMigrateCommand.cs
    [Fact]
    public async Task A_migration_takes_a_session_lock_and_creates_no_lock_table()
    {
        var db = new FakeMySql();
        await using var context = db.Context<ShelfMigrationContext>(o => new ShelfMigrationContext(o));
        db.Connection.EnqueueScalarResult(1L);
        db.Connection.EnqueueScalarResult(0L);

        await context.Database.MigrateAsync(Ct);

        // Named for the database, so migrators of different databases on one server do not wait for each other.
        Assert.Contains(db.Commands, c => c == "SELECT GET_LOCK(CONCAT('__EFMigrationsLock:', MD5(COALESCE(DATABASE(), ''))), 120)");
        Assert.Contains(db.Commands, c => c == "SELECT RELEASE_LOCK(CONCAT('__EFMigrationsLock:', MD5(COALESCE(DATABASE(), ''))))");
        Assert.DoesNotContain(db.Commands, c => c.Contains("CREATE TABLE", StringComparison.Ordinal) && c.Contains("__EFMigrationsLock", StringComparison.Ordinal));
    }

    // A session lock lives on its connection: if EF closed that connection between GET_LOCK and RELEASE_LOCK, the lock would be
    // gone while the migration ran. Each close is recorded with how many commands had run, so a close inside the lock is seen.
    [Fact]
    public async Task The_migrations_connection_stays_open_from_GET_LOCK_until_RELEASE_LOCK()
    {
        var db = new FakeMySql();
        var closedAfter = new List<int>();
        db.Connection.StateChange += (_, e) =>
        {
            if (e.CurrentState == System.Data.ConnectionState.Closed)
            {
                closedAfter.Add(db.Commands.Count);
            }
        };
        await using var context = db.Context<ShelfMigrationContext>(o => new ShelfMigrationContext(o));
        db.Connection.EnqueueScalarResult(1L);
        db.Connection.EnqueueScalarResult(0L);

        await context.Database.MigrateAsync(Ct);

        var commands = db.Commands.ToList();
        var lockAt = commands.FindIndex(c => c.StartsWith("SELECT GET_LOCK", StringComparison.Ordinal));
        var releaseAt = commands.FindIndex(c => c.StartsWith("SELECT RELEASE_LOCK", StringComparison.Ordinal));
        Assert.True(lockAt >= 0 && releaseAt > lockAt);
        Assert.DoesNotContain(closedAfter, n => n > lockAt && n <= releaseAt);
    }

    // GET_LOCK returns 0 when the wait ends without the lock: the migrator must stop before changing anything.
    [Fact]
    public async Task A_migration_refused_the_session_lock_changes_nothing()
    {
        var db = new FakeMySql();
        await using var context = db.Context<ShelfMigrationContext>(o => new ShelfMigrationContext(o));
        db.Connection.EnqueueScalarResult(0L);

        await Assert.ThrowsAsync<TimeoutException>(() => context.Database.MigrateAsync(Ct));

        Assert.DoesNotContain(db.Commands, c => c.StartsWith("CREATE", StringComparison.Ordinal));
        Assert.DoesNotContain(db.Commands, c => c.StartsWith("SELECT RELEASE_LOCK", StringComparison.Ordinal));
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
        Assert.Contains($"{Q}Token{Q} CHAR(36) NOT NULL COLLATE {Q}ascii_bin{Q}", ddl);
        Assert.Contains($"{Q}Location{Q} POINT NOT NULL " + srid, ddl);
        Assert.Contains($"PRIMARY KEY ({Q}Code{Q}(20))", ddl);
        Assert.Contains($"CREATE FULLTEXT INDEX {Q}IX_Place_Name{Q} ON {Q}Place{Q} ({Q}Name{Q}) WITH PARSER {Q}ngram{Q};", ddl);
        Assert.Contains($"CREATE SPATIAL INDEX {Q}IX_Place_Location{Q} ON {Q}Place{Q} ({Q}Location{Q});", ddl);
    }
}
