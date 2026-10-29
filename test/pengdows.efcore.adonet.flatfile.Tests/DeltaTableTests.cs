using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using pengdows.flatfile;

namespace pengdows.efcore.adonet.flatfile.Tests;

/// <summary>
///     Pengdows data-change delta tables, written by <see cref="KeyRetrieval.DeltaTable" /> and run by the engine
///     (<c>SELECT ... FROM FINAL TABLE (INSERT ...)</c>, <c>NEW TABLE (UPDATE ...)</c>, <c>OLD TABLE (DELETE ...)</c>).
///     Flatfile has no identity columns yet, so keys here are the application's: the delta table returns a value the database
///     fills in (a column default), checks each updated row's concurrency token and each deleted row.
/// </summary>
public class DeltaTableTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"adonet-delta-{Guid.NewGuid():N}");

    public DeltaTableTests()
        => Directory.CreateDirectory(_path);

    public void Dispose()
        => Directory.Delete(_path, recursive: true);

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public class Ticket
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [ConcurrencyCheck]
        public string Title { get; set; } = "";

        public string? Status { get; set; }
    }

    private sealed class TicketContext(DbContextOptions<TicketContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ticket>().Property(t => t.Status).HasDefaultValue("open").ValueGeneratedOnAdd();
    }

    private sealed class DeltaTableDialect : AdoNetDialect
    {
        public override string Name => "pengdows.flatfile (delta tables)";
        public override ISqlDialect Sql { get; } = new DeltaTableSql();
        public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities { SupportsMultipleStatementsPerCommand = true };
    }

    private sealed class DeltaTableSql : AnsiSqlDialect
    {
        public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Directory("path");
        public override string ParameterMarker => ":";
        public override bool ParameterNameIncludesMarker => false;
        public override KeyRetrieval Keys => KeyRetrieval.DeltaTable;
    }

    private async Task<TicketContext> CreateAsync(bool createTable = true)
    {
        var context = new TicketContext(new DbContextOptionsBuilder<TicketContext>()
            .UseAdoNet(FlatFileProviderFactory.Instance, $"path={_path}", new DeltaTableDialect())
            .Options);
        if (!createTable)
        {
            return context;
        }

        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"Tickets\" (\"Id\" INTEGER NOT NULL, \"Title\" VARCHAR(100) NOT NULL, "
            + "\"Status\" VARCHAR(20) DEFAULT 'open' NOT NULL, CONSTRAINT \"PK_Tickets\" PRIMARY KEY (\"Id\"))",
            Ct);
        return context;
    }

    [Fact]
    public async Task An_insert_reads_a_database_default_from_the_final_table()
    {
        await using var context = await CreateAsync();
        var ticket = new Ticket { Id = 1, Title = "first" };

        context.Add(ticket);
        await context.SaveChangesAsync(Ct);

        Assert.Equal("open", ticket.Status);
    }

    [Fact]
    public async Task An_update_finds_its_row_in_the_new_table_and_a_stale_token_finds_none()
    {
        await using var context = await CreateAsync();
        context.Add(new Ticket { Id = 1, Title = "first" });
        await context.SaveChangesAsync(Ct);
        context.ChangeTracker.Clear();

        var ticket = await context.Tickets.SingleAsync(Ct);
        ticket.Title = "second";
        await context.SaveChangesAsync(Ct);

        await using var other = await CreateAsync(createTable: false);
        var stale = new Ticket { Id = 1, Title = "first", Status = "open" };
        other.Attach(stale);
        stale.Title = "third";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_delete_finds_its_row_in_the_old_table_and_a_missing_row_finds_none()
    {
        await using var context = await CreateAsync();
        context.Add(new Ticket { Id = 1, Title = "first" });
        await context.SaveChangesAsync(Ct);
        context.ChangeTracker.Clear();

        context.Remove(new Ticket { Id = 1, Title = "first", Status = "open" });
        await context.SaveChangesAsync(Ct);
        Assert.Empty(await context.Tickets.ToListAsync(Ct));

        context.Remove(new Ticket { Id = 1, Title = "first", Status = "open" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }
}
