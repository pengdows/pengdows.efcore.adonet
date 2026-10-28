using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Reverse engineering reads what Microsoft's SQLite model factory reads from sqlite_master and the PRAGMA table functions:
///     AUTOINCREMENT keys, generated columns, unique constraints, descending and partial indexes, and foreign keys naming no
///     column (the principal's key). Over fakeDb's scripted catalog rows, in the order the reader asks for them.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Scaffolding/Internal/SqliteDatabaseModelFactory.cs
/// </summary>
public class SqliteSchemaReaderTests
{
    private static Dictionary<string, object?> Row(params (string Name, object? Value)[] values)
        => values.ToDictionary(v => v.Name, v => v.Value);

    private static Dictionary<string, object?> Column(string name, string type, int pk = 0, int hidden = 0, int notNull = 0)
        => Row(("name", name), ("type", type), ("notnull", notNull), ("dflt_value", null), ("pk", pk), ("hidden", hidden));

    private static DatabaseModel Read()
    {
        using var connection = new fakeDbConnection { ConnectionString = "Data Source=fake;EmulatedProduct=Sqlite" };
        connection.Open();
        connection.EnqueueReaderResult(
        [
            Row(("name", "Blogs"), ("type", "table"), ("sql", "CREATE TABLE \"Blogs\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Blogs\" PRIMARY KEY autoincrement, \"Name\" TEXT, \"Doubled\" INTEGER AS (\"Id\" * 2) STORED)")),
            Row(("name", "Posts"), ("type", "table"), ("sql", "CREATE TABLE \"Posts\" (\"Id\" INTEGER PRIMARY KEY, \"BlogId\" INTEGER REFERENCES \"Blogs\" ON DELETE CASCADE, \"Title\" TEXT, \"Slug\" TEXT UNIQUE, \"Note\" TEXT DEFAULT 'AUTOINCREMENT')")),
            Row(("name", "V"), ("type", "view"), ("sql", "CREATE VIEW \"V\" AS SELECT \"Id\" FROM \"Blogs\""))
        ]);
        connection.EnqueueReaderResult(
        [
            Row(("name", "IX_Posts_Title"), ("sql", "CREATE INDEX \"IX_Posts_Title\" ON \"Posts\" (\"Title\" DESC, \"Slug\") WHERE \"Title\" <> ')' AND \"Slug\" IS NOT NULL"))
        ]);
        connection.EnqueueReaderResult([Column("Id", "INTEGER", pk: 1, notNull: 1), Column("Name", "TEXT"), Column("Doubled", "INTEGER", hidden: 3)]);
        connection.EnqueueReaderResult(new List<Dictionary<string, object?>>());
        connection.EnqueueReaderResult([Column("Id", "INTEGER", pk: 1), Column("BlogId", "INTEGER"), Column("Title", "TEXT"), Column("Slug", "TEXT"), Column("Note", "TEXT")]);
        connection.EnqueueReaderResult(
        [
            Row(("seq", 0), ("name", "sqlite_autoindex_Posts_1"), ("unique", 1), ("origin", "u"), ("partial", 0)),
            Row(("seq", 1), ("name", "IX_Posts_Title"), ("unique", 0), ("origin", "c"), ("partial", 1))
        ]);
        connection.EnqueueReaderResult(
        [
            Row(("seqno", 0), ("cid", 3), ("name", "Slug"), ("desc", 0), ("key", 1)),
            Row(("seqno", 1), ("cid", -1), ("name", null), ("desc", 0), ("key", 0))
        ]);
        connection.EnqueueReaderResult(
        [
            Row(("seqno", 0), ("cid", 2), ("name", "Title"), ("desc", 1), ("key", 1)),
            Row(("seqno", 1), ("cid", 3), ("name", "Slug"), ("desc", 0), ("key", 1)),
            Row(("seqno", 2), ("cid", -1), ("name", null), ("desc", 0), ("key", 0))
        ]);
        connection.EnqueueReaderResult([Column("Id", "INTEGER")]);
        connection.EnqueueReaderResult(new List<Dictionary<string, object?>>());
        connection.EnqueueReaderResult(
        [
            Row(("id", 0), ("seq", 0), ("table", "Blogs"), ("from", "BlogId"), ("to", null), ("on_update", "NO ACTION"), ("on_delete", "CASCADE"))
        ]);

        return SqliteSchemaReader.Instance.Read(connection, new DatabaseModelFactoryOptions(), SqliteDialect.Instance.Sql);
    }

    [Fact]
    public void Only_an_autoincrement_key_is_generated_on_add()
    {
        var model = Read();

        Assert.Equal(ValueGenerated.OnAdd, model.Tables.Single(t => t.Name == "Blogs").Columns.Single(c => c.Name == "Id").ValueGenerated);
        Assert.Null(model.Tables.Single(t => t.Name == "Posts").Columns.Single(c => c.Name == "Id").ValueGenerated);
    }

    [Fact]
    public void A_generated_column_is_computed_and_stored_when_hidden_says_so()
    {
        var doubled = Read().Tables.Single(t => t.Name == "Blogs").Columns.Single(c => c.Name == "Doubled");

        Assert.NotNull(doubled.ComputedColumnSql);
        Assert.True(doubled.IsStored);
    }

    [Fact]
    public void A_unique_column_is_a_unique_constraint_not_an_index()
    {
        var posts = Read().Tables.Single(t => t.Name == "Posts");

        Assert.Equal(["Slug"], Assert.Single(posts.UniqueConstraints).Columns.Select(c => c.Name));
        Assert.Equal(["Id"], posts.PrimaryKey!.Columns.Select(c => c.Name));
    }

    [Fact]
    public void An_index_keeps_its_order_and_only_its_where_clause_as_filter()
    {
        var index = Assert.Single(Read().Tables.Single(t => t.Name == "Posts").Indexes);

        Assert.Equal(["Title", "Slug"], index.Columns.Select(c => c.Name));
        Assert.Equal([true, false], index.IsDescending);
        Assert.Equal("\"Title\" <> ')' AND \"Slug\" IS NOT NULL", index.Filter);
    }

    [Fact]
    public void A_foreign_key_naming_no_column_references_the_principals_key()
    {
        var key = Assert.Single(Read().Tables.Single(t => t.Name == "Posts").ForeignKeys);

        Assert.Equal("Blogs", key.PrincipalTable.Name);
        Assert.Equal(["Id"], key.PrincipalColumns.Select(c => c.Name));
        Assert.Equal(["BlogId"], key.Columns.Select(c => c.Name));
        Assert.Equal(ReferentialAction.Cascade, key.OnDelete);
    }

    [Fact]
    public void A_view_has_its_columns()
    {
        var view = Read().Tables.Single(t => t.Name == "V");

        Assert.IsType<DatabaseView>(view);
        Assert.Equal(["Id"], view.Columns.Select(c => c.Name));
    }

    [Theory]
    [InlineData("CREATE INDEX \"i\" ON \"t\" (\"a\")", null)]
    [InlineData("CREATE UNIQUE INDEX \"i(\" ON \"t)\" (\"a)\", [b(]) WHERE a > 1", "a > 1")]
    [InlineData("create index i on t (lower(a), 'x)') where (b = ')') ", "(b = ')')")]
    public void A_partial_index_filter_is_the_text_after_where(string sql, string? filter)
        => Assert.Equal(filter, SqliteSchemaReader.IndexFilter(sql));
}
