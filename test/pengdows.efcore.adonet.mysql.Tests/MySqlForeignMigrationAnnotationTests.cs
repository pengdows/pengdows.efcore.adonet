using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Migrations an application wrote with Pomelo or with Oracle's MySql.EntityFrameworkCore carry those providers'
///     annotations: Pomelo's "MySql:" keys, Oracle's "MySQL:" keys, and their own enums as values. Moving to this provider, the
///     same migrations must write the same tables, so each is read as this provider's own, a value by its name.
/// </summary>
public class MySqlForeignMigrationAnnotationTests
{
    // Stand-ins for Pomelo's MySqlValueGenerationStrategy and Oracle's MySQLValueGenerationStrategy: only the names count.
    private enum MySqlValueGenerationStrategy { None, IdentityColumn, ComputedColumn }

    private enum MySQLValueGenerationStrategy { None, IdentityColumn, ComputedColumn }

    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);
        return string.Join("\n", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText));
    }

    private static CreateTableOperation Table(Action<AddColumnOperation> id, Action<AddColumnOperation>? name = null)
    {
        var idColumn = new AddColumnOperation { Table = "People", Name = "Id", ClrType = typeof(int), ColumnType = "int" };
        id(idColumn);
        var nameColumn = new AddColumnOperation { Table = "People", Name = "Name", ClrType = typeof(string), ColumnType = "varchar(50)", IsNullable = true };
        name?.Invoke(nameColumn);
        return new CreateTableOperation
        {
            Name = "People",
            Columns = { idColumn, nameColumn },
            PrimaryKey = new AddPrimaryKeyOperation { Table = "People", Name = "PK_People", Columns = ["Id"] }
        };
    }

    [Fact]
    public void Pomelos_identity_strategy_is_auto_increment()
        => Assert.Contains("`Id` int AUTO_INCREMENT NOT NULL", Sql(Table(c => c["MySql:ValueGenerationStrategy"] = MySqlValueGenerationStrategy.IdentityColumn)));

    [Fact]
    public void Oracles_identity_strategy_is_auto_increment()
        => Assert.Contains("`Id` int AUTO_INCREMENT NOT NULL", Sql(Table(c => c["MySQL:ValueGenerationStrategy"] = MySQLValueGenerationStrategy.IdentityColumn)));

    [Fact]
    public void Oracles_legacy_value_generated_on_add_is_auto_increment()
        => Assert.Contains("`Id` int AUTO_INCREMENT NOT NULL", Sql(Table(c => c["MySQL:ValueGeneratedOnAdd"] = true)));

    [Fact]
    public void A_strategy_of_none_is_not_auto_increment()
        => Assert.DoesNotContain("AUTO_INCREMENT", Sql(Table(c => c["MySQL:ValueGenerationStrategy"] = MySQLValueGenerationStrategy.None)));

    [Fact]
    public void Oracles_character_set_is_the_columns()
        => Assert.Contains(
            "`Name` varchar(50) CHARACTER SET latin1",
            Sql(Table(_ => { }, c => c["MySQL:Charset"] = "latin1")));

    [Fact]
    public void Oracles_full_text_index_is_a_fulltext_index()
    {
        var index = new CreateIndexOperation { Name = "IX_People_Name", Table = "People", Columns = ["Name"] };
        index["MySQL:FullTextIndex"] = true;

        Assert.Contains("CREATE FULLTEXT INDEX `IX_People_Name`", Sql(index));
    }
}
