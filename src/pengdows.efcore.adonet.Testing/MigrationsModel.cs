using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Testing;

public class Author
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = "";

    public List<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public decimal Price { get; set; }
    public bool InPrint { get; set; }
    public DateTime Published { get; set; }
    public Guid Isbn { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Schema built by EF from the model (CreateTables) rather than by hand-written DDL.</summary>
public class SchemaContext(DbContextOptions<SchemaContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>().ToTable("SchemaAuthors");
        modelBuilder.Entity<Book>(b =>
        {
            b.ToTable("SchemaBooks");
            b.Property(x => x.Price).HasPrecision(10, 2);
            b.HasIndex(x => x.Title).IsUnique();
        });
    }
}

/// <summary>Schema built by applying the migrations below.</summary>
public class MigrationsContext(DbContextOptions<MigrationsContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(a =>
        {
            a.ToTable("Writers");
            a.Property(x => x.Name).HasColumnName("FullName");
            a.Ignore(x => x.Books);
        });
        modelBuilder.Ignore<Book>();
    }
}

public static class MigrationIds
{
    public const string Initial = "20260101000000_Initial";
    public const string Reshape = "20260102000000_Reshape";
    public const string AlterCode = "20260103000000_AlterCode";
}

/// <summary>Creates a table with an identity key, then a second with a foreign key and indexes.</summary>
[DbContext(typeof(MigrationsContext))]
[Migration(MigrationIds.Initial)]
public class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Authors",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation(AdoNetAnnotationNames.Identity, true),
                Name = t.Column<string>(maxLength: 100, nullable: false)
            },
            constraints: t => t.PrimaryKey("PK_Authors", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Shelves",
            columns: t => new
            {
                Id = t.Column<int>(nullable: false).Annotation(AdoNetAnnotationNames.Identity, true),
                AuthorId = t.Column<int>(nullable: false),
                Label = t.Column<string>(maxLength: 50, nullable: true),
                Code = t.Column<string>(maxLength: 20, nullable: true)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_Shelves", x => x.Id);
                t.ForeignKey("FK_Shelves_Authors", x => x.AuthorId, "Authors", "Id");
            });

        migrationBuilder.CreateIndex("IX_Shelves_AuthorId", "Shelves", "AuthorId");
        migrationBuilder.CreateIndex("IX_Shelves_Code", "Shelves", "Code");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Shelves").Annotation(AdoNetAnnotationNames.IdentityColumns, new[] { "Id" });
        migrationBuilder.DropTable("Authors").Annotation(AdoNetAnnotationNames.IdentityColumns, new[] { "Id" });
    }
}

/// <summary>Renames and adds columns, renames a table, and swaps an index.</summary>
[DbContext(typeof(MigrationsContext))]
[Migration(MigrationIds.Reshape)]
public class ReshapeMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Shelves_Code", "Shelves");
        migrationBuilder.AddColumn<int>("Capacity", "Shelves", nullable: false, defaultValue: 10);
        migrationBuilder.CreateIndex("IX_Shelves_Label", "Shelves", "Label", unique: true);
        migrationBuilder.RenameColumn("Name", "Authors", "FullName");
        migrationBuilder.RenameTable("Authors", newName: "Writers").Annotation(AdoNetAnnotationNames.IdentityColumns, new[] { "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable("Writers", newName: "Authors").Annotation(AdoNetAnnotationNames.IdentityColumns, new[] { "Id" });
        migrationBuilder.RenameColumn("FullName", "Authors", "Name");
        migrationBuilder.DropIndex("IX_Shelves_Label", "Shelves");
        migrationBuilder.DropColumn("Capacity", "Shelves");
        migrationBuilder.CreateIndex("IX_Shelves_Code", "Shelves", "Code");
    }
}

/// <summary>Widens an unindexed column and makes it required with a default.</summary>
[DbContext(typeof(MigrationsContext))]
[Migration(MigrationIds.AlterCode)]
public class AlterCodeMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AlterColumn<string>(
            "Code", "Shelves", maxLength: 40, nullable: false, defaultValue: "none", oldMaxLength: 20, oldNullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.AlterColumn<string>(
            "Code", "Shelves", maxLength: 20, nullable: true, oldMaxLength: 40, oldNullable: false, oldDefaultValue: "none");
}

public static class MigrationsOptions
{
    /// <summary>The test migrations have no model snapshot, which EF otherwise reports as pending model changes.</summary>
    public static DbContextOptionsBuilder<TContext> ForTestMigrations<TContext>(this DbContextOptionsBuilder<TContext> builder)
        where TContext : DbContext
        => builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
}
