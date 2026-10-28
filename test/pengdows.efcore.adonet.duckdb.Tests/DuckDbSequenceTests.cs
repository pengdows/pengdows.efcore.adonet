using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace pengdows.efcore.adonet.duckdb.Tests;

/// <summary>
///     DuckDB has no identity columns: a generated key is <c>DEFAULT nextval</c> of a sequence created before its table and
///     dropped after it, as the DuckDB EF provider does (<c>UseAutoIncrement</c>). EF's own sequences are DuckDB's CREATE
///     SEQUENCE, which takes no type; DuckDB can't alter, restart or rename one, and such a migration is refused by name.
///     SQL shape only. Behaviour reference (not copied):
///     https://github.com/skuirrels/DuckDB.EFCoreProvider/blob/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Migrations/DuckDBMigrationsSqlGenerator.cs
/// </summary>
public class DuckDbSequenceTests
{
    private static ShopContext Context()
        => new(new DbContextOptionsBuilder<ShopContext>().UseAdoNetDuckDb("Data Source=:memory:").Options);

    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = Context();
        return string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText))
            .ReplaceLineEndings("\n");
    }

    [Fact]
    public void A_generated_key_defaults_to_nextval_of_a_sequence_created_before_its_table()
    {
        using var context = Context();
        var script = context.Database.GenerateCreateScript().ReplaceLineEndings("\n");

        Assert.Contains("CREATE SEQUENCE \"Orders_Id_seq\";", script);
        Assert.Contains("\"Id\" INTEGER DEFAULT nextval('\"Orders_Id_seq\"') NOT NULL", script);
        Assert.True(script.IndexOf("CREATE SEQUENCE \"Orders_Id_seq\"", StringComparison.Ordinal) < script.IndexOf("CREATE TABLE \"Orders\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Use_auto_increment_makes_any_integer_column_sequence_generated()
    {
        using var context = Context();
        var property = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Order))!.FindProperty(nameof(Order.Number))!;
        var script = context.Database.GenerateCreateScript();

        Assert.Equal(ValueGenerated.OnAdd, property.ValueGenerated);
        Assert.Contains("CREATE SEQUENCE \"Orders_Number_seq\";", script);
        Assert.Contains("\"Number\" BIGINT DEFAULT nextval('\"Orders_Number_seq\"') NOT NULL", script);
    }

    [Fact]
    public void A_dropped_table_drops_its_key_sequences_after_it()
    {
        var sql = Sql(new DropTableOperation { Name = "Orders", [AdoNetAnnotationNames.IdentityColumns] = new[] { "Id" } });

        Assert.Contains("DROP TABLE \"Orders\";\nDROP SEQUENCE \"Orders_Id_seq\";", sql);
    }

    [Fact]
    public void An_ef_sequence_is_created_without_a_type()
    {
        var sql = Sql(new CreateSequenceOperation { Name = "order_numbers", ClrType = typeof(long), StartValue = 100, IncrementBy = 5, MinValue = 1, IsCyclic = true });

        Assert.StartsWith("CREATE SEQUENCE \"order_numbers\" START WITH 100 INCREMENT BY 5 MINVALUE 1 CYCLE;", sql);
        Assert.DoesNotContain(" AS ", sql);
        Assert.Contains("DROP SEQUENCE \"order_numbers\";", Sql(new DropSequenceOperation { Name = "order_numbers" }));
    }

    [Theory]
    [MemberData(nameof(Unsupported))]
    public void Changing_a_sequence_is_refused_by_name(MigrationOperation operation)
        => Assert.Contains("order_numbers", Assert.Throws<NotSupportedException>(() => Sql(operation)).Message);

    public static TheoryData<MigrationOperation> Unsupported => new()
    {
        new AlterSequenceOperation { Name = "order_numbers", IncrementBy = 2 },
        new RestartSequenceOperation { Name = "order_numbers", StartValue = 1 },
        new RenameSequenceOperation { Name = "order_numbers", NewName = "numbers" }
    };

    public class Order
    {
        public int Id { get; set; }
        public long Number { get; set; }
    }

    public sealed class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().Property(o => o.Number).UseAutoIncrement();
    }
}
