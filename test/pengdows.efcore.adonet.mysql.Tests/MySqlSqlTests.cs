using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.mysql.Tests;

public class MySqlSqlTests
{
    private static BlogContext Context(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>();
        configure(builder);
        return new BlogContext(builder.Options);
    }

    private static string PagedQuery(BlogContext context)
    {
        var name = "x";
        return context.Blogs.Where(b => b.Name == name).OrderBy(b => b.Id).Skip(5).Take(10).ToQueryString();
    }

    [Fact]
    public void Uses_backticks_and_a_limit_for_offset_only()
    {
        using var context = Context(b => b.UseAdoNetMySql("Server=localhost"));

        var sql = context.Blogs.OrderBy(b => b.Id).Skip(5).ToQueryString();

        Assert.Contains("FROM `Blogs` AS `b`", sql);
        Assert.Matches(@"LIMIT 18446744073709551615\s+OFFSET @p", sql);
    }
}

/// <summary>
///     SingleStore speaks MySQL but accepts SAVEPOINT and ROLLBACK TO SAVEPOINT and ignores them, so rolled-back work is
///     committed. MySqlConnector reports savepoints there anyway (it declined to tell servers apart:
///     https://github.com/mysql-net/MySqlConnector/issues/1677), so the dialect says there are none.
/// </summary>
public class SingleStoreSavepointTests
{
    [Fact]
    public async Task SingleStore_has_no_savepoints_whatever_the_driver_reports()
    {
        await using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>()
            .UseAdoNetMySql("Server=localhost", o => o.UseDialect(MySqlDialect.SingleStore))
            .Options);

        Assert.Equal("SingleStore", MySqlDialect.SingleStore.Name);
        Assert.Null(MySqlDialect.SingleStore.Sql.Savepoints);
        Assert.NotNull(MySqlDialect.Instance.Sql.Savepoints);
        Assert.Same(MySqlDialect.SingleStore, context.GetService<IAdoNetDialect>());
    }
}

/// <summary>
///     MySQL takes no literal default on a TEXT, BLOB, JSON or GEOMETRY column, but since 8.0.13 takes an expression default:
///     a default on one of those is written in parentheses; other columns keep the plain literal. Behaviour reference:
///     Pomelo's MySqlMigrationsSqlGenerator.DefaultValue (adapted).
/// </summary>
public class MySqlDefaultValueTests
{
    private static string Sql(string columnType, object? defaultValue = null, string? defaultValueSql = null)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql("Server=localhost").Options);
        var operation = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation
        {
            Table = "T", Name = "C", ClrType = typeof(string), ColumnType = columnType, DefaultValue = defaultValue, DefaultValueSql = defaultValueSql
        };
        return string.Join("", context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate([operation]).Select(c => c.CommandText));
    }

    [Theory]
    [InlineData("LONGTEXT")]
    [InlineData("text")]
    [InlineData("MEDIUMBLOB")]
    [InlineData("JSON")]
    public void A_default_on_a_text_blob_or_json_column_is_an_expression(string columnType)
        => Assert.Contains($"{columnType} DEFAULT ('Banana Joe')", Sql(columnType, "Banana Joe"));

    [Fact]
    public void A_default_sql_on_a_text_column_is_parenthesized_once()
    {
        Assert.Contains("LONGTEXT DEFAULT (UUID())", Sql("LONGTEXT", defaultValueSql: "UUID()"));
        Assert.Contains("LONGTEXT DEFAULT (UUID())", Sql("LONGTEXT", defaultValueSql: "(UUID())"));
    }

    [Fact]
    public void Other_columns_keep_a_plain_literal_default()
        => Assert.Contains("VARCHAR(20) DEFAULT 'Banana Joe'", Sql("VARCHAR(20)", "Banana Joe"));
}

/// <summary>
///     InnoDB wants an AUTO_INCREMENT column to lead an index; in a composite key led by another column (an owned
///     collection's owner key, then its own number) it doesn't, so the column gets a unique constraint of its own. Behaviour
///     reference: Pomelo's MySqlMigrationsSqlGenerator (adapted).
/// </summary>
public class MySqlAutoIncrementKeyTests
{
    private static string Sql(params string[] primaryKey)
    {
        using var context = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseAdoNetMySql("Server=localhost").Options);
        var owner = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation { Table = "Lines", Name = "OwnerId", ClrType = typeof(int) };
        var id = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation { Table = "Lines", Name = "Id", ClrType = typeof(int) };
        id.AddAnnotation(AdoNetAnnotationNames.Identity, true);
        var create = new Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation
        {
            Name = "Lines",
            Columns = { owner, id },
            PrimaryKey = new Microsoft.EntityFrameworkCore.Migrations.Operations.AddPrimaryKeyOperation { Table = "Lines", Name = "PK_Lines", Columns = primaryKey }
        };
        return string.Join("", context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate([create]).Select(c => c.CommandText));
    }

    [Fact]
    public void An_auto_increment_column_after_the_first_key_column_leads_a_unique_constraint()
        => Assert.Contains("UNIQUE (`Id`)", Sql("OwnerId", "Id"));

    [Fact]
    public void An_auto_increment_column_that_leads_the_key_needs_nothing_more()
        => Assert.DoesNotContain("UNIQUE", Sql("Id", "OwnerId"));
}
