using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>The ISO DDL the core writes for migration operations EF's base generator leaves to providers.</summary>
public class MigrationsSqlTests
{
    private static string Sql(IAdoNetDialect dialect, params MigrationOperation[] operations)
    {
        using var context = new FakeDatabase().Context(dialect);
        return string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText))
            .ReplaceLineEndings("\n");
    }

    [Fact]
    public void Sized_types_come_from_the_dialect()
    {
        using var context = new FakeDatabase().Context(AdoNetDialect.Ansi);
        var source = context.GetService<IRelationalTypeMappingSource>();

        Assert.Equal("VARCHAR(40)", source.FindMapping(typeof(string), null, false, null, 40)!.StoreType);
        Assert.Equal("VARCHAR(20)", source.FindMapping(typeof(string), null, false, null, 20)!.StoreType);
        Assert.Equal("DECIMAL(10,2)", source.FindMapping(typeof(decimal), null, false, null, null, false, false, 10, 2)!.StoreType);
        Assert.Equal("VARCHAR", source.FindMapping(typeof(string))!.StoreType);
    }

    [Fact]
    public void Alter_column_writes_one_statement_per_changed_aspect()
    {
        var sql = Sql(
            AdoNetDialect.Ansi,
            new AlterColumnOperation
            {
                Table = "Shelves", Name = "Code", ClrType = typeof(string), MaxLength = 40, IsNullable = false, DefaultValue = "none",
                OldColumn = new AddColumnOperation { Table = "Shelves", Name = "Code", ClrType = typeof(string), MaxLength = 20, IsNullable = true }
            });

        Assert.Equal(
            "ALTER TABLE \"Shelves\" ALTER COLUMN \"Code\" SET DATA TYPE VARCHAR(40);\n"
            + "ALTER TABLE \"Shelves\" ALTER COLUMN \"Code\" SET NOT NULL;\n"
            + "ALTER TABLE \"Shelves\" ALTER COLUMN \"Code\" SET DEFAULT 'none';\n",
            sql);
    }
}
