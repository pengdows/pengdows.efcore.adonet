using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A DateTime row version ([Timestamp] or IsRowVersion) is MySQL's TIMESTAMP(6) DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE
///     CURRENT_TIMESTAMP(6), as Pomelo maps it: the server sets it on every insert and update, EF reads it back, and an update
///     or delete carrying a stale one is a concurrency conflict. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlTypeMappingSource.cs
/// </summary>
public abstract class MySqlRowVersionTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : MySqlFixture
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
        Assert.InRange(inserted, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // TIMESTAMP(6) keeps microseconds: a later update in the same microsecond would keep it.
        await Task.Delay(5, Ct);
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
        public DateTime Version { get; set; }
    }

    public sealed class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }
}

public class MySqlConnectorRowVersionTests(MySqlFixture fixture) : MySqlRowVersionTests<MySqlFixture>(fixture);

public class MySqlDataRowVersionTests(MySqlDataFixture fixture) : MySqlRowVersionTests<MySqlDataFixture>(fixture);

public class MariaDbRowVersionTests(MariaDbFixture fixture) : MySqlRowVersionTests<MariaDbFixture>(fixture);
