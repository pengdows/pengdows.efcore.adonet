using Microsoft.EntityFrameworkCore;
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
}
