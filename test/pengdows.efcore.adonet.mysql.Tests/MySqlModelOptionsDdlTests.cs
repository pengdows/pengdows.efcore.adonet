using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.NetTopologySuite;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Pomelo's model options written into DDL: table options (ENGINE and the rest), a table's collation, a geometry column's
///     spatial reference system, a FULLTEXT index's parser, a key's prefix lengths, a complex property's character set, a
///     collation for GUID columns. SQL shape only, on MySQL and MariaDB, which writes a column's SRID as REF_SYSTEM_ID. The
///     annotations are Pomelo's, so its migration snapshots read the same. Provider-only: these are MySQL's DDL. Behaviour
///     reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Migrations/MySqlMigrationsSqlGenerator.cs
/// </summary>
public class MySqlModelOptionsDdlTests
{
    public sealed class Address
    {
        public string City { get; set; } = "";
    }

    public sealed class Shelf
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Guid Token { get; set; }
        public global::NetTopologySuite.Geometries.Point Location { get; set; } = null!;
        public Address Address { get; set; } = new();
    }

    public sealed class Tag
    {
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
    }

    private sealed class ShelfContext(DbContextOptions<ShelfContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseGuidCollation("ascii_bin");
            modelBuilder.Entity<Shelf>(b =>
            {
                b.HasCharSet("utf8mb4");
                b.UseCollation("utf8mb4_bin");
                b.HasTableOption("ENGINE", "InnoDB");
                b.HasTableOption("ROW_FORMAT", "DYNAMIC");
                b.Property(s => s.Name).HasMaxLength(200);
                b.Property(s => s.Location).HasSpatialReferenceSystem(4326);
                b.HasIndex(s => s.Name).IsFullText(true, "ngram");
                b.ComplexProperty(s => s.Address, c => c.Property(a => a.City).HasMaxLength(50).HasCharSet("latin1"));
            });
            modelBuilder.Entity<Tag>(b =>
            {
                b.Property(t => t.Code).HasMaxLength(500);
                b.Property(t => t.Label).HasMaxLength(500);
                b.HasKey(t => t.Code).HasPrefixLength(20);
                b.HasAlternateKey(t => t.Label).HasPrefixLength(30);
            });
        }
    }

    private static ShelfContext Context(bool mariaDb)
        => new(new DbContextOptionsBuilder<ShelfContext>()
            .UseAdoNetMySql("Server=localhost", o =>
            {
                o.UseDialect(mariaDb ? MySqlDialect.MariaDb : MySqlDialect.Instance);
                o.UseNetTopologySuite();
            })
            .Options);

    private static string Script(bool mariaDb)
    {
        using var context = Context(mariaDb);
        return context.Database.GenerateCreateScript();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Table_options_and_collation_follow_the_table(bool mariaDb)
        => Assert.Contains(") DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin ENGINE=InnoDB ROW_FORMAT=DYNAMIC;", Script(mariaDb));

    [Fact]
    public void A_spatial_reference_system_is_the_columns_SRID_on_MySql()
        => Assert.Matches($"{Q}Location{Q} \\w+ NOT NULL SRID 4326", Script(mariaDb: false));

    [Fact]
    public void A_spatial_reference_system_is_the_columns_REF_SYSTEM_ID_on_MariaDb()
    {
        var script = Script(mariaDb: true);

        Assert.Matches($"{Q}Location{Q} \\w+ NOT NULL REF_SYSTEM_ID=4326", script);
        Assert.DoesNotContain("SRID", script);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_fulltext_index_names_its_parser(bool mariaDb)
        => Assert.Contains($"CREATE FULLTEXT INDEX {Q}IX_Shelf_Name{Q} ON {Q}Shelf{Q} ({Q}Name{Q}) WITH PARSER {Q}ngram{Q};", Script(mariaDb));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_keys_prefix_lengths_index_its_columns_first_characters(bool mariaDb)
    {
        var script = Script(mariaDb);

        Assert.Contains($"PRIMARY KEY ({Q}Code{Q}(20))", script);
        Assert.Contains($"UNIQUE ({Q}Label{Q}(30))", script);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_complex_propertys_character_set_and_the_guid_collation(bool mariaDb)
    {
        var script = Script(mariaDb);

        Assert.Contains($"{Q}Address_City{Q} VARCHAR(50) CHARACTER SET latin1 NOT NULL", script);
        Assert.Contains($"{Q}Token{Q} CHAR(36) NOT NULL COLLATE {Q}ascii_bin{Q}", script);
        // The entity type's character set and collation reach its text columns (Pomelo's delegation); not the GUID collation.
        Assert.Contains($"{Q}Name{Q} VARCHAR(200) CHARACTER SET utf8mb4 NOT NULL COLLATE {Q}utf8mb4_bin{Q},", script);
    }

    // Changed options, character set and collation of an existing table: ALTER TABLE with what changed.
    [Fact]
    public void Altered_table_options_and_collation_are_altered()
    {
        using var context = Context(mariaDb: false);
        var operation = new AlterTableOperation { Name = "Shelves" };
        operation.OldTable.AddAnnotation("MySql:StoreOptions", "ENGINE=InnoDB,");
        operation.AddAnnotation("MySql:StoreOptions", "ENGINE=MyISAM,ROW_FORMAT=COMPACT,");
        operation.OldTable.AddAnnotation("MySql:CharSet", "latin1");
        operation.AddAnnotation("MySql:CharSet", "utf8mb4");
        operation.AddAnnotation("Relational:Collation", "utf8mb4_bin");

        var sql = string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate([operation]).Select(c => c.CommandText));

        Assert.Contains($"ALTER TABLE {Q}Shelves{Q} CHARACTER SET utf8mb4 COLLATE utf8mb4_bin;", sql);
        Assert.Contains($"ALTER TABLE {Q}Shelves{Q} ENGINE=MyISAM ROW_FORMAT=COMPACT;", sql);
    }

    [Theory]
    [InlineData("ENGINE; DROP TABLE x", "InnoDB")]
    [InlineData("ENGINE", "InnoDB; DROP TABLE x")]
    [InlineData("ENGINE", "InnoDB -- comment")]
    [InlineData("", "InnoDB")]
    public void A_table_option_that_is_not_one_is_refused(string name, string value)
    {
        var builder = new ModelBuilder();

        Assert.Throws<ArgumentException>(() => builder.Entity<Tag>().HasTableOption(name, value));
    }

    [Fact]
    public void A_table_option_value_keeps_its_commas()
    {
        var builder = new ModelBuilder();
        builder.Entity<Tag>().HasTableOption("COMMENT", "'a, b'").HasTableOption("ENGINE", "InnoDB");

        Assert.Equal("COMMENT='a,, b',ENGINE=InnoDB,", builder.Model.FindEntityType(typeof(Tag))!["MySql:StoreOptions"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_database_is_MySql_whatever_the_variant(bool mariaDb)
    {
        using var context = Context(mariaDb);

        Assert.True(context.Database.IsMySql());
    }
}
