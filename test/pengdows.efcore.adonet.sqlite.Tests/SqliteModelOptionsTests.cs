using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.sqlite.Tests;

/// <summary>
///     Microsoft's SQLite model options: <c>UseSqlReturningClause(false)</c> on a table with triggers or a virtual table, which
///     then reads its keys with <c>last_insert_rowid()</c> and checks rows with <c>changes()</c>; <c>UseAutoincrement</c>; and
///     <c>IsSqlite</c>. Over fakeDb emulating SQLite, so the statements are scripted and never run. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Extensions/SqliteTableBuilderExtensions.cs
/// </summary>
public class SqliteModelOptionsTests
{
    private readonly fakeDbConnection _connection = new() { ConnectionString = "Data Source=fake;EmulatedProduct=Sqlite" };
    private readonly List<string> _commands = [];

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private TriggeredContext Context()
        => new(new DbContextOptionsBuilder<TriggeredContext>()
            .UseAdoNetSqlite(_connection)
            .LogTo(m => _commands.Add(m.ReplaceLineEndings("\n")), [RelationalEventId.CommandExecuted])
            .Options);

    [Fact]
    public async Task A_table_without_the_returning_clause_reads_its_key_with_last_insert_rowid()
    {
        await using var context = Context();
        _connection.EnqueueReaderResult([new Dictionary<string, object?> { ["Id"] = 5 }]);
        var audited = new Audited { Name = "x" };

        context.Add(audited);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(5, audited.Id);
        var command = Assert.Single(_commands);
        Assert.DoesNotContain("RETURNING", command);
        Assert.Contains("WHERE changes() = 1 AND \"Id\" = last_insert_rowid();", command);
    }

    [Fact]
    public async Task Other_tables_keep_returning()
    {
        await using var context = Context();
        _connection.EnqueueReaderResult([new Dictionary<string, object?> { ["Id"] = 6 }]);

        context.Add(new Plain { Name = "x" });
        await context.SaveChangesAsync(Ct);

        Assert.Contains("RETURNING \"Id\"", Assert.Single(_commands));
    }

    // A model written against this provider's earlier annotation name: still leaves out RETURNING.
    [Fact]
    public async Task A_table_marked_with_the_legacy_annotation_name_still_leaves_out_returning()
    {
        await using var context = new LegacyTriggeredContext(new DbContextOptionsBuilder<LegacyTriggeredContext>()
            .UseAdoNetSqlite(_connection)
            .LogTo(m => _commands.Add(m.ReplaceLineEndings("\n")), [RelationalEventId.CommandExecuted])
            .Options);
        _connection.EnqueueReaderResult([new Dictionary<string, object?> { ["Id"] = 7 }]);
        var audited = new Audited { Name = "x" };

        context.Add(audited);
        await context.SaveChangesAsync(Ct);

        Assert.Equal(7, audited.Id);
        var command = Assert.Single(_commands);
        Assert.DoesNotContain("RETURNING", command);
        Assert.False(context.Model.FindEntityType(typeof(Audited))!.IsSqlReturningClauseUsed());
    }

    [Fact]
    public void The_option_is_read_back_and_can_be_turned_on_again()
    {
        using var context = Context();
        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.False(model.FindEntityType(typeof(Audited))!.IsSqlReturningClauseUsed());
        Assert.True(model.FindEntityType(typeof(Plain))!.IsSqlReturningClauseUsed());
        Assert.True(model.FindEntityType(typeof(Reenabled))!.IsSqlReturningClauseUsed());
    }

    [Fact]
    public void The_option_is_stored_under_the_upstream_annotation_name()
    {
        using var context = Context();
        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.Equal(false, model.FindEntityType(typeof(Audited))!.FindAnnotation("Sqlite:UseSqlReturningClause")?.Value);
        Assert.Null(model.FindEntityType(typeof(Audited))!.FindAnnotation("AdoNet:UseReturningClause"));
    }

    [Fact]
    public void Use_autoincrement_makes_the_column_the_tables_autoincrement_key()
    {
        using var context = Context();
        var property = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Reenabled))!.FindProperty(nameof(Reenabled.Id))!;

        Assert.Equal(ValueGenerated.OnAdd, property.ValueGenerated);
        Assert.Equal(true, property[AdoNetAnnotationNames.Identity]);
    }

    [Fact]
    public void The_context_says_whether_it_is_sqlite()
    {
        using var sqlite = Context();
        using var other = new TriggeredContext(new DbContextOptionsBuilder<TriggeredContext>().UseAdoNet(_connection, contextOwnsConnection: false, o => o.UseDialect(AdoNetDialect.Ansi)).Options);

        Assert.True(sqlite.Database.IsSqlite());
        Assert.False(other.Database.IsSqlite());
    }

    // An entity split across two tables, one of them with triggers: only that table's statements leave out RETURNING.
    [Fact]
    public async Task One_split_table_without_the_returning_clause_checks_its_rows_with_changes()
    {
        await using var context = new SplitContext(new DbContextOptionsBuilder<SplitContext>()
            .UseAdoNetSqlite(_connection)
            .LogTo(m => _commands.Add(m.ReplaceLineEndings("\n")), [RelationalEventId.CommandExecuted])
            .Options);
        context.Remove(new Plain { Id = 2 });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));

        var command = Assert.Single(_commands);
        var details = command[command.IndexOf("DELETE FROM \"PlainDetails\"", StringComparison.Ordinal)..].Split("DELETE FROM \"Plain\"\n")[0];
        Assert.Contains("SELECT changes();", details);
        Assert.DoesNotContain("RETURNING", details);
        Assert.Contains("RETURNING 1", command[command.IndexOf("DELETE FROM \"Plain\"\n", StringComparison.Ordinal)..].Split("DELETE FROM \"PlainDetails\"")[0]);
        Assert.False(context.Model.FindEntityType(typeof(Plain))!.FindMappingFragment(StoreObjectIdentifier.Table("PlainDetails"))!["Sqlite:UseSqlReturningClause"] as bool?);
    }

    public sealed class SplitContext(DbContextOptions<SplitContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Plain>().SplitToTable("PlainDetails", t =>
            {
                t.Property(p => p.Name);
                t.UseSqlReturningClause(false);
            });
    }

    public class Audited
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class Plain
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
    }

    public class Reenabled
    {
        public int Id { get; set; }
    }

    public sealed class LegacyTriggeredContext(DbContextOptions<LegacyTriggeredContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Audited>().HasAnnotation("AdoNet:UseReturningClause", false);
    }

    public sealed class TriggeredContext(DbContextOptions<TriggeredContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Audited>().ToTable(t => t.UseSqlReturningClause(false));
            modelBuilder.Entity<Plain>();
            modelBuilder.Entity<Reenabled>().Property(p => p.Id).UseAutoincrement();
            modelBuilder.Entity<Reenabled>().ToTable(t => t.UseSqlReturningClause(false)).ToTable(t => t.UseSqlReturningClause());
        }
    }
}
