using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     A byte[] row version ([Timestamp] or IsRowVersion) is SQL Server's rowversion: the database sets it on every insert and
///     update, EF reads it back, and an update or delete carrying a stale one is a concurrency conflict.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerTypeMappingSource.cs
/// </summary>
public class SqlServerRowVersionTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IAsyncLifetime
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
    public async Task The_database_sets_the_row_version_and_a_stale_one_is_a_conflict()
    {
        var note = new Note { Text = "a" };
        await using (var context = Context())
        {
            context.Add(note);
            await context.SaveChangesAsync(Ct);
        }

        var inserted = note.Version;
        Assert.NotNull(inserted);
        Assert.Equal(8, inserted.Length);

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
        public byte[] Version { get; set; } = null!;
    }

    public sealed class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }
}
