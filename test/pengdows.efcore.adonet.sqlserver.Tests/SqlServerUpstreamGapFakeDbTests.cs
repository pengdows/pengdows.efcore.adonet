using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>Failing parity probes for SQL Server provider-owned metadata not yet covered by fakeDb.</summary>
public sealed class SqlServerUpstreamGapFakeDbTests
{
    [Fact]
    public void Timestamp_property_uses_sql_server_rowversion_storage()
    {
        var db = new FakeSqlServer();
        using var context = db.Context<NoteContext>(options => new NoteContext(options));

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("rowversion", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Temporal_table_annotations_generate_system_versioning_ddl()
    {
        var db = new FakeSqlServer();
        using var context = db.Context<TemporalContext>(options => new TemporalContext(options));

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("SYSTEM_VERSIONING", script, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Note
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        [Timestamp]
        public byte[] Version { get; set; } = [];
    }

    private sealed class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    private sealed class TemporalContext(DbContextOptions<TemporalContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Note>().HasAnnotation("SqlServer:IsTemporal", true);
    }
}
