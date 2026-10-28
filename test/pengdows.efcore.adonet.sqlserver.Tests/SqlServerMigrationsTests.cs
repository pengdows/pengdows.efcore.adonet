using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.sqlserver.Tests;

public class SqlServerMigrationsTests
{
    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetSqlServer("Server=localhost").Options);
        return string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText))
            .ReplaceLineEndings("\n");
    }

    [Fact]
    public void Moving_a_table_to_another_schema_transfers_it()
    {
        Assert.Equal(
            "ALTER SCHEMA [b] TRANSFER [a].[T];\n",
            Sql(new RenameTableOperation { Schema = "a", Name = "T", NewSchema = "b", NewName = "T" }));
        Assert.Equal(
            "ALTER SCHEMA [b] TRANSFER [a].[T];\nEXEC sp_rename N'[b].[T]', N'U';\n",
            Sql(new RenameTableOperation { Schema = "a", Name = "T", NewSchema = "b", NewName = "U" }));
        Assert.Equal(
            "ALTER SCHEMA [dbo] TRANSFER [a].[T];\n",
            Sql(new RenameTableOperation { Schema = "a", Name = "T", NewSchema = null, NewName = "T" }));
    }

    [Fact]
    public void Restrict_foreign_keys_use_sql_servers_no_action_spelling()
    {
        var operation = new AddForeignKeyOperation
        {
            Name = "FK_Child_Parent",
            Table = "Child",
            Schema = "dbo",
            Columns = ["ParentId"],
            PrincipalTable = "Parent",
            PrincipalSchema = "dbo",
            PrincipalColumns = ["Id"],
            OnDelete = ReferentialAction.Restrict
        };

        Assert.Equal(
            "ALTER TABLE [dbo].[Child] ADD CONSTRAINT [FK_Child_Parent] FOREIGN KEY ([ParentId]) REFERENCES [dbo].[Parent] ([Id]) ON DELETE NO ACTION;\n",
            Sql(operation));
    }

    [Fact]
    public void Computed_columns_use_t_sql_as_and_persisted()
    {
        Assert.Equal(
            "ALTER TABLE [T] ADD [Total] AS ([A] + [B]) PERSISTED;\n",
            Sql(new AddColumnOperation { Table = "T", Name = "Total", ClrType = typeof(int), ColumnType = "int", ComputedColumnSql = "[A] + [B]", IsStored = true }));
        Assert.Equal(
            "ALTER TABLE [T] ADD [Label] AS (N'x' + [Name]) COLLATE [Latin1_General_BIN2];\n",
            Sql(new AddColumnOperation
            {
                Table = "T", Name = "Label", ClrType = typeof(string), ColumnType = "nvarchar(max)", ComputedColumnSql = "N'x' + [Name]",
                Collation = "Latin1_General_BIN2"
            }));
    }

    /// <summary>
    ///     Seed data naming an identity column runs with IDENTITY_INSERT on for that table only, its name delimited however
    ///     hostile; seed data leaving the key to the database doesn't touch it.
    /// </summary>
    [Fact]
    public void Seed_data_with_identity_values_switches_identity_insert_for_its_table()
    {
        using var context = new HostileTableContext(
            new DbContextOptionsBuilder<HostileTableContext>().UseAdoNetSqlServer("Server=localhost").Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var model = context.GetService<IDesignTimeModel>().Model;

        var withKeys = string.Join("", generator.Generate(
            [new InsertDataOperation { Schema = "s'x", Table = "we]ird", Columns = ["Id", "Name"], Values = new object[,] { { 7, "a" } } }],
            model).Select(c => c.CommandText)).ReplaceLineEndings("\n");
        var withoutKeys = string.Join("", generator.Generate(
            [new InsertDataOperation { Schema = "s'x", Table = "we]ird", Columns = ["Name"], Values = new object[,] { { "a" } } }],
            model).Select(c => c.CommandText)).ReplaceLineEndings("\n");

        Assert.StartsWith("SET IDENTITY_INSERT [s'x].[we]]ird] ON;\nINSERT INTO [s'x].[we]]ird] ([Id], [Name])", withKeys);
        Assert.EndsWith("SET IDENTITY_INSERT [s'x].[we]]ird] OFF;\n", withKeys);
        Assert.DoesNotContain("IDENTITY_INSERT", withoutKeys);
    }

    private sealed class HostileTable
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class HostileTableContext(DbContextOptions<HostileTableContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HostileTable>().ToTable("we]ird", "s'x");
    }
}
