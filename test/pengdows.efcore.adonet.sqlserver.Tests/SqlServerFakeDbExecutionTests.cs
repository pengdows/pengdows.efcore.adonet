using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     SQL Server compatibility-level and rowversion behavior executed through fakeDb. The fake reader supplies the values
///     a real SQL Server would return, while EF still generates and consumes the complete provider command.
/// </summary>
public sealed class SqlServerFakeDbExecutionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Compatibility_level_160_trim_query_reads_scripted_rows()
    {
        var db = new FakeSqlServer();
        await using var context = db.Context<RowContext>(options => new RowContext(options));
        db.Returns([9]);

        var ids = await context.Rows
            .Where(row => row.Name.TrimStart('x', 'y') == "a")
            .Select(row => row.Id)
            .ToListAsync(Ct);

        Assert.Equal([9], ids);
        Assert.Contains("LTRIM", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rowversion_values_are_materialized_from_the_provider_reader()
    {
        var db = new FakeSqlServer();
        await using var context = db.Context<VersionContext>(options => new VersionContext(options));
        var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        db.Returns([4, "note", version]);

        var note = await context.Notes.SingleAsync(Ct);

        Assert.Equal(4, note.Id);
        Assert.Equal("note", note.Text);
        Assert.Equal(version, note.Version);
        Assert.Contains("[Version]", Assert.Single(db.Commands));
    }

    [Fact]
    public async Task Primitive_collections_use_openjson_and_materialize_scripted_rows()
    {
        var db = new FakeSqlServer();
        await using var context = db.Context<CollectionContext>(options => new CollectionContext(options));
        db.Returns([12]);

        var ids = await context.Rows.Where(row => row.Tags.Contains(3)).Select(row => row.Id).ToListAsync(Ct);

        Assert.Equal([12], ids);
        Assert.Contains("OPENJSON", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_tables_executes_sql_server_ddl_through_fakeDb()
    {
        var db = new FakeSqlServer();
        using var context = db.Context<CollectionContext>(options => new CollectionContext(options));
        var creator = context.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();

        creator.CreateTables();

        Assert.Contains("CREATE TABLE", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class RowContext(DbContextOptions<RowContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    private sealed class Note
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        [Timestamp]
        public byte[] Version { get; set; } = [];
    }

    private sealed class VersionContext(DbContextOptions<VersionContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    private sealed class CollectionRow
    {
        public int Id { get; set; }
        public int[] Tags { get; set; } = [];
    }

    private sealed class CollectionContext(DbContextOptions<CollectionContext> options) : DbContext(options)
    {
        public DbSet<CollectionRow> Rows => Set<CollectionRow>();
    }
}
