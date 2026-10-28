using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     A table that can't take the dialect's RETURNING clause (a table with triggers, or a virtual table) is annotated
///     <see cref="AdoNetAnnotationNames.UseReturningClause" /> false; its keys come back through the dialect's
///     <see cref="ISqlDialect.KeysWithoutReturning" />, as Microsoft's SQLite provider does for UseSqlReturningClause(false).
///     Other tables keep RETURNING, and a dialect without the fallback keeps it for every table.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Update/Internal/SqliteUpdateSqlGenerator.cs
/// </summary>
public class ReturningClauseTests
{
    private static readonly IAdoNetDialect WithFallback = new PartsDialect(
        "Returning with a fallback",
        new FallbackSql(),
        new DatabaseCapabilities { SupportsMultipleStatementsPerCommand = true, DefaultMaxBatchSize = 10 });

    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_without_the_returning_clause_reads_its_key_with_a_select_after_the_insert()
    {
        await using var context = _db.ContextOf<NoReturningContext>(WithFallback, o => new NoReturningContext(o));
        var item = new Item { Name = "n" };
        _db.Returns(FakeDatabase.Row("Id", 9));

        context.Add(item);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(9, item.Id);
        var command = Assert.Single(_db.Commands);
        Assert.DoesNotContain("RETURNING", command);
        Assert.Contains("SELECT \"Id\"\nFROM \"Items\"\nWHERE changes() = 1 AND \"Id\" = last_insert_rowid();", command);
    }

    [Fact]
    public async Task Deletes_from_such_a_table_check_the_rows_affected_function()
    {
        await using var context = _db.ContextOf<NoReturningContext>(WithFallback, o => new NoReturningContext(o));
        context.Remove(new Item { Id = 2 });
        _db.Returns(FakeDatabase.Row("count", 1));

        await context.SaveChangesAsync(Ct);

        var command = Assert.Single(_db.Commands);
        Assert.Contains("DELETE FROM \"Items\"", command);
        Assert.EndsWith("SELECT changes();", command.TrimEnd());
    }

    [Fact]
    public async Task Other_tables_keep_returning()
    {
        await using var context = _db.ContextOf<NoReturningContext>(WithFallback, o => new NoReturningContext(o));
        _db.Returns(FakeDatabase.Row("Id", 3));

        context.Add(new Part { Label = "p", ItemId = 1 });
        await context.SaveChangesAsync(Ct);

        Assert.Contains("RETURNING \"Id\"", Assert.Single(_db.Commands));
    }

    [Fact]
    public async Task A_dialect_without_a_fallback_keeps_returning_for_every_table()
    {
        await using var context = _db.ContextOf<NoReturningContext>(TestDialects.Ansi, o => new NoReturningContext(o));
        _db.Returns(FakeDatabase.Row("Id", 7));

        context.Add(new Item { Name = "n" });
        await context.SaveChangesAsync(Ct);

        Assert.Contains("RETURNING \"Id\"", Assert.Single(_db.Commands));
    }

    // Entity splitting: the annotation on one table's mapping fragment applies to that table alone.
    [Fact]
    public async Task A_split_table_without_the_returning_clause_checks_rows_with_the_rows_affected_function()
    {
        await using var context = _db.ContextOf<SplitContext>(WithFallback, o => new SplitContext(o));
        context.Remove(new Item { Id = 2 });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));

        var command = Assert.Single(_db.Commands);
        var details = command[command.IndexOf("DELETE FROM \"ItemDetails\"", StringComparison.Ordinal)..];
        var items = command[command.IndexOf("DELETE FROM \"Item\"\n", StringComparison.Ordinal)..];
        Assert.Contains("SELECT changes();", details.Split("DELETE FROM \"Item\"\n")[0]);
        Assert.DoesNotContain("RETURNING", details.Split("DELETE FROM \"Item\"\n")[0]);
        Assert.Contains("RETURNING 1", items.Split("DELETE FROM \"ItemDetails\"")[0]);
    }

    public sealed class SplitContext(DbContextOptions<SplitContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>().Ignore(i => i.Parts);
            modelBuilder.Entity<Item>().SplitToTable(
                "ItemDetails",
                t =>
                {
                    t.Property(i => i.Name);
                    t.MappingFragment.SetAnnotation(AdoNetAnnotationNames.UseReturningClause, false);
                });
        }
    }

    private sealed class FallbackSql : AnsiSqlDialect
    {
        public override SelectAfterKeyRetrieval? KeysWithoutReturning { get; } = KeyRetrieval.SelectAfter("last_insert_rowid()", "changes()");
    }

    public sealed class NoReturningContext(DbContextOptions<NoReturningContext> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        public DbSet<Part> Parts => Set<Part>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>().HasAnnotation(AdoNetAnnotationNames.UseReturningClause, false);
    }
}
