using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     A uint row version ([Timestamp] or IsRowVersion) is PostgreSQL's xmin system column, as Npgsql maps it: every row has
///     it and every change sets it, so the table declares nothing; EF reads it back, and an update or delete carrying a stale
///     one is a concurrency conflict. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Metadata/Conventions/NpgsqlPostgresModelFinalizingConvention.cs
/// </summary>
public class PostgreSqlRowVersionTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private readonly string _connectionString = fixture.ConnectionStringFor("rowversion_" + Guid.NewGuid().ToString("N")[..12]);

    private NoteContext Context()
    {
        var context = fixture.CreateContext<NoteContext>(o => new NoteContext(o));
        context.Database.SetConnectionString(_connectionString);
        return context;
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureCreatedAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = Context();
        await context.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task The_server_sets_the_row_version_and_a_stale_one_is_a_conflict()
    {
        var note = new Note { Text = "a" };
        await using (var context = Context())
        {
            context.Add(note);
            await context.SaveChangesAsync(Ct);
        }

        var inserted = note.Version;
        Assert.NotEqual(0u, inserted);

        await using (var context = Context())
        {
            context.Attach(note);
            note.Text = "b";
            await context.SaveChangesAsync(Ct);
        }

        Assert.NotEqual(inserted, note.Version);

        await using (var stale = Context())
        {
            stale.Attach(new Note { Id = note.Id, Text = "b", Version = inserted }).Property(n => n.Text).IsModified = true;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
        }

        await using (var stale = Context())
        {
            stale.Remove(new Note { Id = note.Id, Version = inserted });
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
        }
    }

    public sealed class Note
    {
        public int Id { get; set; }
        public string? Text { get; set; }

        [Timestamp]
        public uint Version { get; set; }
    }

    public sealed class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }
}
