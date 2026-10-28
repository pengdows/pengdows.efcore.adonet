using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     SQLite's ALTER TABLE renames and adds columns, nothing more. A constraint added to a table created in the same
///     migration goes into its CREATE TABLE (SQLite checks a foreign key's table when it's used, not when it's declared);
///     any other change of constraints or columns rebuilds the table from the migration's target model, keeping its rows.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Migrations/SqliteMigrationsSqlGenerator.cs
/// </summary>
public class SqliteMigrationsTests
{
    // A table name with every character SQLite's delimiters and literals treat specially.
    private const string Table = "we\"ird'Shelf]";
    private const string Temporary = "\"ef_temp_we\"\"ird'Shelf]\"";
    private const string Delimited = "\"we\"\"ird'Shelf]\"";

    private static (IMigrationsSqlGenerator Generator, IModel Model) Generator()
    {
        var context = new ShelfContext(new DbContextOptionsBuilder<ShelfContext>().UseAdoNetSqlite("Data Source=:memory:").Options);
        return (context.GetService<IMigrationsSqlGenerator>(), context.GetService<IDesignTimeModel>().Model);
    }

    private static string Sql(IReadOnlyList<MigrationOperation> operations, bool withModel = true)
    {
        var (generator, model) = Generator();
        return string.Join("", generator.Generate(operations, withModel ? model : null).Select(c => c.CommandText)).ReplaceLineEndings("\n");
    }

    // A generated key of one column is written inline as PRIMARY KEY AUTOINCREMENT, which SQLite allows only there: without it
    // the key is a plain rowid, and the next insert reuses the highest key after a delete (EF Core's fix-up then gives the
    // new row the deleted one's navigations).
    [Fact]
    public void A_generated_single_column_key_is_an_autoincrement_primary_key()
    {
        var sql = Sql(
        [
            new CreateTableOperation
            {
                Name = "Counters",
                Columns =
                {
                    new AddColumnOperation
                    {
                        Name = "Id", Table = "Counters", ClrType = typeof(int), ColumnType = "INTEGER",
                        [AdoNetAnnotationNames.Identity] = true
                    },
                    new AddColumnOperation { Name = "Label", Table = "Counters", ClrType = typeof(string), IsNullable = true }
                },
                PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_Counters", Table = "Counters", Columns = ["Id"] }
            }
        ], withModel: false);

        Assert.Contains("\"Id\" INTEGER CONSTRAINT \"PK_Counters\" PRIMARY KEY AUTOINCREMENT NOT NULL", sql);
        Assert.Equal(1, sql.Split("PRIMARY KEY").Length - 1);
    }

    [Fact]
    public void A_foreign_key_closing_a_cycle_goes_into_the_create_table_of_the_same_migration()
    {
        var sql = Sql(
        [
            new CreateTableOperation
            {
                Name = "Households",
                Columns = { new AddColumnOperation { Name = "Id", Table = "Households", ClrType = typeof(int) }, new AddColumnOperation { Name = "HeadId", Table = "Households", ClrType = typeof(int), IsNullable = true } }
            },
            new CreateTableOperation
            {
                Name = "People",
                Columns = { new AddColumnOperation { Name = "Id", Table = "People", ClrType = typeof(int) } }
            },
            new AddForeignKeyOperation
            {
                Name = "FK_Households_People", Table = "Households", Columns = ["HeadId"], PrincipalTable = "People", PrincipalColumns = ["Id"]
            }
        ], withModel: false);

        Assert.DoesNotContain("ALTER TABLE", sql);
        Assert.Contains("CONSTRAINT \"FK_Households_People\" FOREIGN KEY (\"HeadId\") REFERENCES \"People\" (\"Id\")", sql);
        Assert.True(sql.IndexOf("FK_Households_People", StringComparison.Ordinal) < sql.IndexOf("CREATE TABLE \"People\"", StringComparison.Ordinal));
    }

    [Fact]
    public void A_foreign_key_added_to_an_existing_table_rebuilds_it_and_keeps_its_rows_and_indexes()
    {
        var sql = Sql(
        [
            new AddForeignKeyOperation
            {
                Name = "FK_Shelf_Owner", Table = Table, Columns = ["OwnerId"], PrincipalTable = "Owners", PrincipalColumns = ["Id"]
            }
        ]);

        Assert.DoesNotContain("ADD CONSTRAINT", sql);
        Assert.Equal(
        [
            "PRAGMA foreign_keys = OFF;",
            $"CREATE TABLE {Temporary} (",
            $"INSERT INTO {Temporary} (\"Id\", \"Label\", \"OwnerId\") SELECT \"Id\", \"Label\", \"OwnerId\" FROM {Delimited};",
            $"DROP TABLE {Delimited};",
            $"ALTER TABLE {Temporary} RENAME TO {Delimited};",
            "PRAGMA foreign_keys = ON;",
            $"CREATE INDEX \"IX_Shelf_Label\" ON {Delimited} (\"Label\");",
            $"CREATE INDEX \"IX_we\"\"ird'Shelf]_OwnerId\" ON {Delimited} (\"OwnerId\");"
        ], Landmarks(sql));
        Assert.Contains("CONSTRAINT \"FK_Shelf_Owner\" FOREIGN KEY (\"OwnerId\") REFERENCES \"Owners\" (\"Id\")", sql);
    }

    [Fact]
    public void A_rebuild_copies_renamed_columns_from_their_old_names_and_leaves_added_ones_to_their_defaults()
    {
        var sql = Sql(
        [
            new RenameColumnOperation { Table = Table, Name = "Name", NewName = "Label" },
            new AddColumnOperation { Table = Table, Name = "OwnerId", ClrType = typeof(int), IsNullable = true },
            new DropColumnOperation { Table = Table, Name = "Obsolete" },
            new AlterColumnOperation { Table = Table, Name = "Label", ClrType = typeof(string), IsNullable = false, DefaultValue = "none" }
        ]);

        Assert.Single(Landmarks(sql), l => l.StartsWith("CREATE TABLE", StringComparison.Ordinal));
        Assert.Contains($"INSERT INTO {Temporary} (\"Id\", \"Label\") SELECT \"Id\", \"Name\" FROM {Delimited};", sql);
        Assert.DoesNotContain("RENAME COLUMN", sql);
        Assert.DoesNotContain("DROP COLUMN", sql);
    }

    public static TheoryData<MigrationOperation> ChangesNeedingARebuild
        => new()
        {
            new DropForeignKeyOperation { Table = Table, Name = "FK_Shelf_Owner" },
            new AlterColumnOperation
            {
                Table = Table, Name = "Label", ClrType = typeof(string), IsNullable = true,
                OldColumn = new AddColumnOperation { Table = Table, Name = "Label", ClrType = typeof(string) }
            }
        };

    [Theory]
    [MemberData(nameof(ChangesNeedingARebuild))]
    public void A_rebuild_without_the_target_model_is_refused_by_name(MigrationOperation operation)
    {
        var refused = Assert.Throws<NotSupportedException>(() => Sql([operation], withModel: false));

        Assert.Contains(operation.GetType().Name, refused.Message);
        Assert.Contains(Table, refused.Message);
    }

    // The statements that show a rebuild's order, each from the start of its line.
    private static List<string> Landmarks(string sql)
        => sql.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("PRAGMA", StringComparison.Ordinal) || l.StartsWith("CREATE TABLE", StringComparison.Ordinal)
                || l.StartsWith("INSERT", StringComparison.Ordinal) || l.StartsWith("DROP", StringComparison.Ordinal)
                || l.StartsWith("ALTER", StringComparison.Ordinal) || l.StartsWith("CREATE INDEX", StringComparison.Ordinal))
            .Select(l => l.StartsWith("CREATE TABLE", StringComparison.Ordinal) ? l[..(l.IndexOf('(') + 1)] : l)
            .ToList();

    private sealed class Owner
    {
        public int Id { get; set; }
    }

    private sealed class Shelf
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public int? OwnerId { get; set; }
        public Owner? Owner { get; set; }
    }

    private sealed class ShelfContext(DbContextOptions<ShelfContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Owner>().ToTable("Owners");
            modelBuilder.Entity<Shelf>(b =>
            {
                b.ToTable(Table);
                b.HasIndex(s => s.Label).HasDatabaseName("IX_Shelf_Label");
                b.HasOne(s => s.Owner).WithMany().HasForeignKey(s => s.OwnerId).HasConstraintName("FK_Shelf_Owner");
            });
        }
    }
}
