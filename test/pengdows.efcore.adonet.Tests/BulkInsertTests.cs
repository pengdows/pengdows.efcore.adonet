using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.Tests;

/// <summary>BulkInsert and the core's multi-row INSERT loader, on fakeDb.</summary>
public class BulkInsertTests
{
    private static readonly IAdoNetDialect TwoRowsPerInsert = new PartsDialect(
        "Two rows per insert", capabilities: new DatabaseCapabilities(MaxParametersPerCommand: 4));

    private static readonly IAdoNetDialect OneParameterPerInsert = new PartsDialect(
        "One parameter per insert", capabilities: new DatabaseCapabilities(MaxParametersPerCommand: 1));

    private static readonly IAdoNetDialect EmptyRowInsert = new PartsDialect("Empty row insert", new PartsSql(defaultValues: "() VALUES ()"));

    private readonly FakeDatabase _db = new();

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    private BulkContext Context(IAdoNetDialect dialect, List<string>? log = null)
        => new(new DbContextOptionsBuilder<BulkContext>()
            .UseAdoNet(_db.Connection, contextOwnsConnection: false, o => o.UseDialect(dialect))
            .EnableSensitiveDataLogging()
            .LogTo(m => log?.Add(m.ReplaceLineEndings("\n").Replace("\n      ", "\n", StringComparison.Ordinal)), [RelationalEventId.CommandExecuted])
            .AllowManyDialects()
            .Options);

    [Fact]
    public async Task Rows_are_inserted_in_chunks_sized_by_the_parameter_limit()
    {
        var log = new List<string>();
        await using var context = Context(TwoRowsPerInsert, log);

        var loaded = await context.BulkInsertAsync(Enumerable.Range(0, 5).Select(i => new Tag { Name = "t" + i, Weight = i }), Ct);

        Assert.Equal(5, loaded);
        var inserts = log.Where(l => l.Contains("INSERT INTO")).ToList();
        Assert.Equal(3, inserts.Count);
        Assert.Contains("INSERT INTO \"Tags\" (\"Name\", \"Weight\")\nVALUES (@p0, @p1),\n(@p2, @p3)", inserts[0]);
        Assert.Contains("@p0='t4'", inserts[2]);
    }

    [Fact]
    public void A_load_without_a_transaction_gets_its_own()
    {
        using var context = Context(AdoNetDialect.Ansi);

        context.BulkInsert([new Tag { Name = "a" }]);

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(System.Data.ConnectionState.Closed, _db.Connection.State);
    }

    [Fact]
    public async Task A_load_inside_a_transaction_joins_it()
    {
        await using var context = Context(AdoNetDialect.Ansi);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);

        await context.BulkInsertAsync([new Tag { Name = "a" }], Ct);
        context.BulkInsert([new Tag { Name = "b" }]);

        Assert.Same(transaction, context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Tables_with_only_a_generated_key_insert_default_values()
    {
        var log = new List<string>();
        await using var context = Context(AdoNetDialect.Ansi, log);

        await context.BulkInsertAsync([new KeyOnly(), new KeyOnly()], Ct);

        Assert.Equal(2, log.Count(l => l.EndsWith("INSERT INTO \"KeyOnly\" DEFAULT VALUES", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Sequence_keys_are_drawn_before_the_load_and_set_on_the_entities()
    {
        var log = new List<string>();
        await using var context = Context(TestDialects.Sequence, log);
        var tags = new[] { new Tag { Name = "a" }, new Tag { Name = "b" } };
        _db.Connection.EnqueueScalarResult(41);
        _db.Connection.EnqueueScalarResult(42L);

        await context.BulkInsertAsync(tags, Ct);
        _db.Connection.EnqueueScalarResult(43);
        context.BulkInsert([new Tag { Name = "c" }]);

        Assert.Equal([41, 42], tags.Select(t => t.Id));
        Assert.Contains("INSERT INTO \"Tags\" (\"Id\", \"Name\", \"Weight\")", log.Single(l => l.Contains("@p3='42'")));
    }

    [Fact]
    public async Task Computed_columns_are_left_to_the_database_and_field_only_keys_are_set()
    {
        var log = new List<string>();
        await using var context = Context(TestDialects.Sequence, log);
        var stamped = new Stamped { Value = 3 };
        _db.Connection.EnqueueScalarResult(7);

        await context.BulkInsertAsync([stamped], Ct);

        Assert.Equal(7, stamped.Id);
        Assert.Contains("INSERT INTO \"Stamped\" (\"Id\", \"Value\")", log.Single(l => l.Contains("INSERT INTO")));
    }

    [Fact]
    public async Task Values_go_through_converters_and_discriminators()
    {
        var log = new List<string>();
        await using var context = Context(AdoNetDialect.Ansi, log);

        await context.BulkInsertAsync([new Dog { Name = "rex", Shouty = "woof" }], Ct);

        var insert = log.Single(l => l.Contains("INSERT INTO \"Animals\""));
        Assert.Contains("'Dog'", insert);
        Assert.Contains("'WOOF'", insert);
    }

    [Fact]
    public async Task Converters_apply_exactly_once()
    {
        var log = new List<string>();
        await using var context = Context(AdoNetDialect.Ansi, log);

        await context.BulkInsertAsync([new Labelled { Code = "a", Size = Size.Large }], Ct);

        var insert = log.Single(l => l.Contains("INSERT INTO \"Labelled\""));
        Assert.Contains("'#a'", insert);
        Assert.DoesNotContain("'##a'", insert);
        Assert.Contains("'Large'", insert);
    }

    [Fact]
    public async Task Shadow_properties_other_than_the_discriminator_are_refused()
    {
        var log = new List<string>();
        await using var context = Context(AdoNetDialect.Ansi, log);
        var thread = new Thread { Id = 1 };

        var refused = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.BulkInsertAsync([new Comment { Thread = thread }], Ct));

        Assert.Contains("ThreadId", refused.Message);
        Assert.Empty(log);
    }

    [Fact]
    public void An_entity_wider_than_the_parameter_limit_is_refused_before_anything_runs()
    {
        var log = new List<string>();
        using var context = Context(OneParameterPerInsert, log);

        var refused = Assert.Throws<NotSupportedException>(() => context.BulkInsert([new Tag { Name = "a" }]));

        Assert.Contains("2 columns", refused.Message);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Rows_with_no_columns_to_write_use_the_dialects_default_values_form()
    {
        var log = new List<string>();
        await using var context = Context(EmptyRowInsert, log);

        await context.BulkInsertAsync([new KeyOnly()], Ct);

        Assert.Single(log, l => l.EndsWith("INSERT INTO \"KeyOnly\" () VALUES ()", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveChanges_of_a_row_with_no_columns_to_write_uses_the_dialects_default_values_form()
    {
        var log = new List<string>();
        await using var context = Context(EmptyRowInsert, log);
        _db.Returns(FakeDatabase.Row("Id", 5));

        context.Add(new KeyOnly());
        await context.SaveChangesAsync(Ct);

        Assert.Single(log, l => l.Contains("INSERT INTO \"KeyOnly\"\n() VALUES ()\nRETURNING \"Id\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_load_leaves_no_drawn_keys_on_the_entities()
    {
        var failing = new PartsDialect(
            "Failing loader", new PartsSql(keys: KeyRetrieval.Sequence()), services: s => s.AddScoped<IAdoNetBulkLoader, FailingLoader>());
        await using var context = Context(failing);
        var tags = new[] { new Tag { Name = "a" }, new Tag { Id = 9, Name = "b" } };
        _db.Connection.EnqueueScalarResult(41);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.BulkInsertAsync(tags, Ct));
        _db.Connection.EnqueueScalarResult(43);
        Assert.Throws<InvalidOperationException>(() => context.BulkInsert([tags[0]]));

        Assert.Equal([0, 9], tags.Select(t => t.Id));
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Unsupported_shapes_are_refused()
    {
        await using var context = Context(AdoNetDialect.Ansi);

        await Assert.ThrowsAsync<NotSupportedException>(() => context.BulkInsertAsync([new Owner()], Ct));
        Assert.Throws<InvalidOperationException>(() => context.BulkInsert([new NotMapped()]));
    }

    [Fact]
    public void A_registered_loader_replaces_the_insert_loader()
    {
        using var context = Context(new PartsDialect("Custom loader", services: s => s.AddScoped<IAdoNetBulkLoader, RecordingLoader>()));

        context.BulkInsert([new Tag { Name = "a" }, new Tag { Name = "b" }]);

        var load = ((RecordingLoader)context.GetService<IAdoNetBulkLoader>()).Loads.Single();
        Assert.Equal("Tags", load.Table);
        Assert.Equal(["Name", "Weight"], load.Columns.Select(c => c.Name));
        Assert.Equal(2, load.Rows.Count);
        Assert.Same(context.Database.GetDbConnection(), load.Connection);
    }

    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
    }

    public class KeyOnly
    {
        public int Id { get; set; }
    }

    public class Animal
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class Dog : Animal
    {
        public string Shouty { get; set; } = "";
    }

    public class Owner
    {
        public int Id { get; set; }
        public Address Address { get; set; } = new();
    }

    public class Address
    {
        public string Street { get; set; } = "";
    }

    public class NotMapped;

    public enum Size
    {
        Small,
        Large
    }

    /// <summary>A converter that isn't idempotent, and an enum stored as its name.</summary>
    public class Labelled
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public Size Size { get; set; }
    }

    public class Thread
    {
        public int Id { get; set; }
    }

    /// <summary>The foreign key to its thread is a shadow property.</summary>
    public class Comment
    {
        public int Id { get; set; }
        public Thread Thread { get; set; } = null!;
    }

    /// <summary>A key reachable only through its field, and a computed column.</summary>
    public class Stamped
    {
#pragma warning disable CS0649 // Set by EF through the field.
        private int _id;
#pragma warning restore CS0649

        public int Id => _id;
        public int Value { get; set; }
        public int Doubled { get; set; }
    }

    public class BulkContext(DbContextOptions<BulkContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tag>().ToTable("Tags");
            modelBuilder.Entity<KeyOnly>().ToTable("KeyOnly");
            modelBuilder.Entity<Animal>().ToTable("Animals");
            modelBuilder.Entity<Dog>().Property(d => d.Shouty).HasConversion(v => v.ToUpperInvariant(), v => v);
            modelBuilder.Entity<Owner>().OwnsOne(o => o.Address);
            modelBuilder.Entity<Labelled>(e =>
            {
                e.ToTable("Labelled");
                e.Property(x => x.Code).HasConversion(v => "#" + v, v => v.Substring(1));
                e.Property(x => x.Size).HasConversion<string>();
            });
            modelBuilder.Entity<Thread>().ToTable("Threads");

            modelBuilder.Entity<Comment>().ToTable("Comments");
            modelBuilder.Entity<Stamped>(e =>
            {
                e.ToTable("Stamped");
                e.Property(x => x.Id).HasField("_id");
                e.Property(x => x.Doubled).HasComputedColumnSql("\"Value\" * 2");
            });
        }
    }

    private sealed class FailingLoader : IAdoNetBulkLoader
    {
        public void Load(BulkLoad load)
            => throw new InvalidOperationException("The load failed.");

        public Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The load failed.");
    }

    private sealed class RecordingLoader : IAdoNetBulkLoader
    {
        public List<BulkLoad> Loads { get; } = [];

        public void Load(BulkLoad load)
            => Loads.Add(load);

        public Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default)
        {
            Loads.Add(load);
            return Task.CompletedTask;
        }
    }
}
