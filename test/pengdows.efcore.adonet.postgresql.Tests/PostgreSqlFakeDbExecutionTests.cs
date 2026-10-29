using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     PostgreSQL provider behavior executed through fakeDb: JSONB parameters and JSON path reads are sent through EF's
///     command pipeline and scripted rows are materialized back into the entity projection.
/// </summary>
public sealed class PostgreSqlFakeDbExecutionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jsonb_comparison_sends_a_jsonb_parameter_and_reads_the_matching_row()
    {
        var db = new FakePostgreSql();
        await using var context = db.Context<Context>(options => new Context(options));
        const string document = "{\"name\":\"x\"}";
        db.Returns([17]);

        var ids = await context.Documents
            .Where(row => row.Document == document)
            .Select(row => row.Id)
            .ToListAsync(Ct);

        Assert.Equal([17], ids);
        var sql = Assert.Single(db.Commands);
        Assert.Contains("\"Document\"", sql);
        Assert.Contains("=", sql);
        Assert.Contains("name", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Jsonb_path_projection_materializes_the_scripted_scalar()
    {
        var db = new FakePostgreSql();
        await using var context = db.Context<JsonContext>(options => new JsonContext(options));
        db.Returns(["Oslo", 3]);

        var value = await context.Documents
            .Select(row => new { Name = row.Details.Name, Count = row.Details.Count })
            .SingleAsync(Ct);

        Assert.Equal(("Oslo", 3), (value.Name, value.Count));
        var sql = Assert.Single(db.Commands);
        Assert.Contains("jsonb_path_query_first", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS INTEGER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Xmin_rowversion_is_selected_and_materialized()
    {
        var db = new FakePostgreSql();
        await using var context = db.Context<VersionContext>(options => new VersionContext(options));
        db.Returns([4, "note", 42u]);

        var note = await context.Notes.SingleAsync(Ct);

        Assert.Equal((4, "note", 42u), (note.Id, note.Text, note.Version));
        Assert.Contains("xmin", Assert.Single(db.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Interval_arithmetic_is_sent_to_the_reader_pipeline()
    {
        var db = new FakePostgreSql();
        await using var context = db.Context<TemporalContext>(options => new TemporalContext(options));
        db.Returns([8]);

        var ids = await context.Rows
            .Where(row => row.Value.AddSeconds(1) > row.Value && row.Span.Milliseconds == 1)
            .Select(row => row.Id)
            .ToListAsync(Ct);

        Assert.Equal([8], ids);
        var sql = Assert.Single(db.Commands);
        Assert.Contains("INTERVAL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXTRACT(MILLISECONDS", sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<RawDocument> Documents => Set<RawDocument>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RawDocument>().Property(row => row.Document).HasColumnType("jsonb");
    }

    private sealed class JsonContext(DbContextOptions<JsonContext> options) : DbContext(options)
    {
        public DbSet<JsonDocument> Documents => Set<JsonDocument>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<JsonDocument>().OwnsOne(row => row.Details, owned => owned.ToJson());
    }

    private sealed class RawDocument
    {
        public int Id { get; set; }
        public string Document { get; set; } = "";
    }

    private sealed class JsonDocument
    {
        public int Id { get; set; }
        public Details Details { get; set; } = new();
    }

    private sealed class Details
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private sealed class Note
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        [Timestamp]
        public uint Version { get; set; }
    }

    private sealed class VersionContext(DbContextOptions<VersionContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    private sealed class TemporalRow
    {
        public int Id { get; set; }
        public DateTimeOffset Value { get; set; }
        public TimeSpan Span { get; set; }
    }

    private sealed class TemporalContext(DbContextOptions<TemporalContext> options) : DbContext(options)
    {
        public DbSet<TemporalRow> Rows => Set<TemporalRow>();
    }
}
