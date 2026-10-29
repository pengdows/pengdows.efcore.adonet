using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
/// Provider-neutral EF Core behaviors which must be exercised against a command-capable fake database.
/// The fake supplies the server result; EF still builds the model, translates the expression, binds
/// parameters, executes the command, and materializes the result.
/// </summary>
public sealed class FakeDbEfCoreConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Any_all_and_contains_use_the_scalar_reader_pipeline()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ConformanceContext>(AdoNetDialect.Ansi, options => new ConformanceContext(options));
        database.Returns(FakeDatabase.Row("Value", true));
        Assert.True(await context.Items.AnyAsync(item => item.Active, Ct));

        database.Returns(FakeDatabase.Row("Value", true));
        Assert.True(await context.Items.AllAsync(item => item.Id > 0, Ct));

        database.Returns(FakeDatabase.Row("Value", true));
        Assert.True(await context.Items.Select(item => item.Id).ContainsAsync(3, Ct));

        Assert.Equal(3, database.Commands.Count);
        Assert.Contains("EXISTS", database.Commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", database.Commands[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IN", database.Commands[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Null_comparison_translation_materializes_the_scripted_entity()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ConformanceContext>(AdoNetDialect.Ansi, options => new ConformanceContext(options));
        database.Returns(FakeDatabase.Row("Id", 4));

        var matches = await context.Items.Where(item => item.Name == null).Select(item => item.Id).SingleAsync(Ct);

        Assert.Equal(4, matches);
        Assert.Contains("IS NULL", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Join_projection_reads_columns_from_one_fakeDb_result_set()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ConformanceContext>(AdoNetDialect.Ansi, options => new ConformanceContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 8, ["Name"] = "joined" });

        var result = await (from item in context.Items
                            join part in context.Parts on item.Id equals part.ItemId
                            select new { item.Id, item.Name }).SingleAsync(Ct);

        Assert.Equal((8, "joined"), (result.Id, result.Name));
        Assert.Contains("JOIN", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Owned_entity_materialization_and_relationship_fixup_run_after_execution()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<OwnedContext>(AdoNetDialect.Ansi, options => new OwnedContext(options));
        database.Returns(new Dictionary<string, object?>
        {
            ["Id"] = 9,
            ["Name"] = "owner",
            ["Address_City"] = "Chicago",
            ["Address_Street"] = "Main"
        });

        var owner = await context.Owners.SingleAsync(Ct);

        Assert.Equal((9, "owner", "Chicago", "Main"),
            (owner.Id, owner.Name, owner.Address.City, owner.Address.Street));
    }

    [Fact]
    public async Task Enum_value_converter_reads_provider_value_and_writes_converted_parameter()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ConvertedContext>(AdoNetDialect.Ansi, options => new ConvertedContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 12, ["State"] = "Ready" });

        var item = await context.Items.SingleAsync(Ct);

        Assert.Equal(Status.Ready, item.State);
        context.Add(new ConvertedItem { Id = 13, State = Status.Done });
        database.Returns(FakeDatabase.Row("Id", 13));
        await context.SaveChangesAsync(Ct);

        Assert.Contains("INSERT", database.Commands[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Composite_keys_are_used_for_tracking_identity()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<CompositeContext>(AdoNetDialect.Ansi, options => new CompositeContext(options));
        database.Returns(
            new Dictionary<string, object?> { ["TenantId"] = 2, ["Code"] = "A", ["Value"] = 1 },
            new Dictionary<string, object?> { ["TenantId"] = 2, ["Code"] = "A", ["Value"] = 2 });

        var rows = await context.Entries.ToListAsync(Ct);

        Assert.Equal(2, rows.Count);
        Assert.Same(rows[0], rows[1]);
        Assert.Single(context.ChangeTracker.Entries<CompositeEntry>());
    }

    [Fact]
    public async Task Query_tags_are_preserved_on_the_command_sent_to_fakeDb()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ConformanceContext>(AdoNetDialect.Ansi, options => new ConformanceContext(options));
        database.Returns(FakeDatabase.Row("Value", 1));

        await context.Items.TagWith("conformance-test").Select(item => item.Id).SingleAsync(Ct);

        Assert.Contains("conformance-test", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task Find_uses_the_fakeDb_once_then_returns_the_tracked_entity()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<FindContext>(AdoNetDialect.Ansi, options => new FindContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 22, ["Name"] = "found" });

        var first = await context.Items.FindAsync([22], Ct);
        var second = await context.Items.FindAsync([22], Ct);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Single(database.Commands);
    }

    [Fact]
    public async Task Explicit_collection_loading_executes_a_second_fakeDb_command_and_fixes_up_the_relationship()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<RelationshipContext>(AdoNetDialect.Ansi, options => new RelationshipContext(options));
        database.Returns(FakeDatabase.Row("Id", 5));
        var parent = await context.Parents.SingleAsync(Ct);

        database.Returns(new Dictionary<string, object?> { ["Id"] = 6, ["Name"] = "child", ["ParentId"] = 5 });
        await context.Entry(parent).Collection(item => item.Children).LoadAsync(Ct);

        var child = Assert.Single(parent.Children);
        Assert.Same(parent, child.Parent);
        Assert.Equal(2, database.Commands.Count);
    }

    [Fact]
    public async Task Plain_no_tracking_queries_do_not_identity_resolve_duplicate_rows()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<FindContext>(AdoNetDialect.Ansi, options => new FindContext(options));
        database.Returns(
            new Dictionary<string, object?> { ["Id"] = 31, ["Name"] = "duplicate" },
            new Dictionary<string, object?> { ["Id"] = 31, ["Name"] = "duplicate" });

        var rows = await context.Items.AsNoTracking().ToListAsync(Ct);

        Assert.Equal(2, rows.Count);
        Assert.NotSame(rows[0], rows[1]);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Detect_changes_transitions_an_attached_entity_to_modified_before_save()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<FindContext>(AdoNetDialect.Ansi, options => new FindContext(options));
        var item = new FindItem { Id = 40, Name = "before" };
        context.Attach(item);

        item.Name = "after";
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Modified, context.Entry(item).State);
    }

    [Fact]
    public async Task Complex_property_projections_are_read_from_fakeDb_without_entity_tracking()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ComplexContext>(AdoNetDialect.Ansi, options => new ComplexContext(options));
        database.Returns(FakeDatabase.Row("City", "Chicago"));

        var city = await context.Items.Select(item => item.Address.City).SingleAsync(Ct);

        Assert.Equal("Chicago", city);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Tpt_queries_join_base_and_derived_tables_before_materialization()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<TptContext>(AdoNetDialect.Ansi, options => new TptContext(options));
        database.Returns(new Dictionary<string, object?> { ["Id"] = 71, ["Name"] = "employee", ["Department"] = "engineering" });

        var employee = await context.People.OfType<TptEmployee>().SingleAsync(Ct);

        Assert.Equal((71, "employee", "engineering"), (employee.Id, employee.Name, employee.Department));
        Assert.Contains("JOIN", Assert.Single(database.Commands), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ConformanceContext(DbContextOptions<ConformanceContext> options) : DbContext(options)
    {
        public DbSet<ConformanceItem> Items => Set<ConformanceItem>();
        public DbSet<ConformancePart> Parts => Set<ConformancePart>();
    }

    private sealed class ConformanceItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool Active { get; set; }
    }

    private sealed class ConformancePart
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
    }

    private sealed class OwnedContext(DbContextOptions<OwnedContext> options) : DbContext(options)
    {
        public DbSet<OwnedOwner> Owners => Set<OwnedOwner>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnedOwner>().OwnsOne(owner => owner.Address);
    }

    private sealed class OwnedOwner
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public OwnedAddress Address { get; set; } = new();
    }

    private sealed class OwnedAddress
    {
        public string City { get; set; } = "";
        public string Street { get; set; } = "";
    }

    private enum Status { New, Ready, Done }

    private sealed class ConvertedContext(DbContextOptions<ConvertedContext> options) : DbContext(options)
    {
        public DbSet<ConvertedItem> Items => Set<ConvertedItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedItem>().Property(item => item.State).HasConversion<string>();
    }

    private sealed class ConvertedItem
    {
        public int Id { get; set; }
        public Status State { get; set; }
    }

    private sealed class CompositeContext(DbContextOptions<CompositeContext> options) : DbContext(options)
    {
        public DbSet<CompositeEntry> Entries => Set<CompositeEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeEntry>().HasKey(entry => new { entry.TenantId, entry.Code });
    }

    private sealed class CompositeEntry
    {
        public int TenantId { get; set; }
        public string Code { get; set; } = "";
        public int Value { get; set; }
    }

    private sealed class FindContext(DbContextOptions<FindContext> options) : DbContext(options)
    {
        public DbSet<FindItem> Items => Set<FindItem>();
    }

    private sealed class FindItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class RelationshipContext(DbContextOptions<RelationshipContext> options) : DbContext(options)
    {
        public DbSet<RelationshipParent> Parents => Set<RelationshipParent>();
        public DbSet<RelationshipChild> Children => Set<RelationshipChild>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RelationshipChild>().HasOne(child => child.Parent)
                .WithMany(parent => parent.Children)
                .HasForeignKey(child => child.ParentId);
    }

    private sealed class RelationshipParent
    {
        public int Id { get; set; }
        public List<RelationshipChild> Children { get; set; } = [];
    }

    private sealed class RelationshipChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Name { get; set; } = "";
        public RelationshipParent Parent { get; set; } = null!;
    }

    private sealed class ComplexContext(DbContextOptions<ComplexContext> options) : DbContext(options)
    {
        public DbSet<ComplexItem> Items => Set<ComplexItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexItem>().ComplexProperty(item => item.Address);
    }

    private sealed class ComplexItem
    {
        public int Id { get; set; }
        public ComplexAddress Address { get; set; } = new();
    }

    private sealed class ComplexAddress
    {
        public string City { get; set; } = "";
    }

    private sealed class TptContext(DbContextOptions<TptContext> options) : DbContext(options)
    {
        public DbSet<TptPerson> People => Set<TptPerson>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TptPerson>().ToTable("People");
            modelBuilder.Entity<TptEmployee>().ToTable("Employees");
        }
    }

    private class TptPerson
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class TptEmployee : TptPerson
    {
        public string Department { get; set; } = "";
    }
}
