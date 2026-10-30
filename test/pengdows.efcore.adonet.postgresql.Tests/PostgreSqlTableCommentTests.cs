using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Testing;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A migration's table comment is PostgreSQL's COMMENT ON TABLE, and a cleared comment is IS NULL. The core generator has
///     no table comment, so without the PostgreSQL override the comment was dropped without an error.
/// </summary>
public class PostgreSqlTableCommentTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Alter_table_sets_and_clears_the_table_comment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var generator = context.GetService<IMigrationsSqlGenerator>();

        await ExecuteAsync(context, generator, new AlterTableOperation { Name = "Blogs", Comment = "Blog table" }, ct);
        Assert.Equal("Blog table", await CommentAsync(context, ct));

        await ExecuteAsync(context, generator, new AlterTableOperation { Name = "Blogs", Comment = null }, ct);
        Assert.Null(await CommentAsync(context, ct));
    }

    [Fact]
    public async Task Alter_column_sets_and_clears_the_column_comment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var generator = context.GetService<IMigrationsSqlGenerator>();

        await ExecuteAsync(context, generator, new AlterColumnOperation { Table = "Blogs", Name = "Name", ClrType = typeof(string), ColumnType = "text", Comment = "Blog name", OldColumn = OldName(null) }, ct);
        Assert.Equal("Blog name", await ColumnCommentAsync(context, ct));

        await ExecuteAsync(context, generator, new AlterColumnOperation { Table = "Blogs", Name = "Name", ClrType = typeof(string), ColumnType = "text", Comment = null, OldColumn = OldName("Blog name") }, ct);
        Assert.Null(await ColumnCommentAsync(context, ct));
    }

    private static AlterColumnOperation OldName(string? comment)
        => new() { Table = "Blogs", Name = "Name", ClrType = typeof(string), ColumnType = "text", Comment = comment };

    private static Task<string?> ColumnCommentAsync(DbContext context, CancellationToken ct)
        => context.Database.SqlQueryRaw<string?>(
                "SELECT col_description('\"Blogs\"'::regclass, (SELECT attnum FROM pg_attribute WHERE attrelid = '\"Blogs\"'::regclass AND attname = 'Name')) AS \"Value\"")
            .SingleAsync(ct);

    private static async Task ExecuteAsync(DbContext context, IMigrationsSqlGenerator generator, MigrationOperation operation, CancellationToken ct)
    {
        foreach (var command in generator.Generate([operation]))
        {
            await context.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        }
    }

    private static Task<string?> CommentAsync(DbContext context, CancellationToken ct)
        => context.Database.SqlQueryRaw<string?>(
                "SELECT obj_description('\"Blogs\"'::regclass, 'pg_class') AS \"Value\"")
            .SingleAsync(ct);
}
