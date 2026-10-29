using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore.Sqlite.Tests;

/// <summary>
///     Microsoft's <c>UseAutoincrement</c>: an integer key is AUTOINCREMENT in SQLite's DDL, and its strategy reads back as
///     <c>SqliteValueGenerationStrategy.Autoincrement</c>.
/// </summary>
public class SqliteUpstreamValueGenerationTests
{
    public sealed class Counter
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class AutoincrementContext(DbContextOptions<AutoincrementContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Counter>(b => b.Property(x => x.Id).UseAutoincrement());
    }

    public sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        public DbSet<Counter> Counters => Set<Counter>();
    }

    private static DbContextOptions<T> Options<T>() where T : DbContext
        => new DbContextOptionsBuilder<T>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)).UseSqlite("Data Source=:memory:").Options;

    [Fact]
    public void UseAutoincrement_sets_the_autoincrement_strategy()
    {
        using var context = new AutoincrementContext(Options<AutoincrementContext>());

        var property = context.Model.FindEntityType(typeof(Counter))!.FindProperty(nameof(Counter.Id))!;

        Assert.Equal(SqliteValueGenerationStrategy.Autoincrement, property.GetValueGenerationStrategy());
    }

    [Fact]
    public void An_autoincrement_key_is_created_with_AUTOINCREMENT()
    {
        using var context = new AutoincrementContext(Options<AutoincrementContext>());

        Assert.Contains("AUTOINCREMENT", context.Database.GenerateCreateScript());
    }

    // A documented divergence, not parity: this provider's integer identity key is AUTOINCREMENT by default, so a deleted
    // row's key is never reused (SqliteMigrations). Microsoft's EF writes the plain rowid unless UseAutoincrement is set. The
    // strategy itself still reads None for such a key.
    [Fact]
    public void A_key_without_UseAutoincrement_has_no_strategy_and_is_AUTOINCREMENT_by_this_providers_default()
    {
        using var context = new PlainContext(Options<PlainContext>());

        var property = context.Model.FindEntityType(typeof(Counter))!.FindProperty(nameof(Counter.Id))!;

        Assert.Equal(SqliteValueGenerationStrategy.None, property.GetValueGenerationStrategy());
        Assert.Contains("AUTOINCREMENT", context.Database.GenerateCreateScript());
    }
}
