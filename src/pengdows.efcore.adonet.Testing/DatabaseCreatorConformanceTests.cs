using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Creating, finding and deleting a database, the same way on every database: a server database through an administrative
///     connection, a file or directory database on disk, as the dialect's <see cref="DatabaseLifecycle" /> declares.
/// </summary>
/// <remarks>
///     Covers the upstream providers' database-creator tests (SQL Server's and Npgsql's <c>DatabaseCreator*Test</c>
///     classes, Microsoft's <c>SqliteDatabaseCreatorTest</c>). Their contracts differ, and this one takes the stricter:
///     <c>Create</c> refuses a database that exists and <c>Delete</c> one that doesn't (as on SQL Server and PostgreSQL;
///     Microsoft's SQLite provider does neither), while <c>EnsureCreated</c> and <c>EnsureDeleted</c> are idempotent.
///     Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite and DuckDB. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/test/EFCore.PG.FunctionalTests/NpgsqlDatabaseCreatorTest.cs
/// </remarks>
public abstract class DatabaseCreatorConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    private readonly List<string> _created = [];

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();
        if (Fixture.ExpectedDialect.Sql.Databases == null)
        {
            Fixture.SkipFor("the dialect can't create or delete databases");
        }

        return ValueTask.CompletedTask;
    }

    // Each test's database is its own, removed afterwards if the test left it behind.
    public async ValueTask DisposeAsync()
    {
        foreach (var connectionString in _created)
        {
            await using var context = Context(connectionString);
            await context.Database.EnsureDeletedAsync();
        }
    }

    private string NewDatabase()
    {
        var connectionString = Fixture.ConnectionStringFor("lifecycle_" + Guid.NewGuid().ToString("N")[..12]);
        _created.Add(connectionString);
        return connectionString;
    }

    private BlogContext Context(string connectionString)
    {
        var context = Fixture.CreateContext();
        context.Database.SetConnectionString(connectionString);
        return context;
    }

    private static IRelationalDatabaseCreator Creator(DbContext context)
        => context.GetService<IRelationalDatabaseCreator>();

    // An owned collection's key is (owner, generated Id): a database that generates a column of a key of several numbers
    // the lines; one that can't refuses the row, as with its own EF Core provider (efcore#26708).
    [Fact]
    public async Task Owned_collection_lines_are_numbered_only_where_the_database_generates_a_composite_key_column()
    {
        var connectionString = NewDatabase();
        await using (var context = Fixture.CreateContext<OwnedLinesContext>(o => new OwnedLinesContext(o)))
        {
            context.Database.SetConnectionString(connectionString);
            await context.Database.EnsureCreatedAsync(Ct);
            context.Add(new LineOwner { Lines = [new OwnedLine { Text = "a" }, new OwnedLine { Text = "b" }] });

            if (!Fixture.ExpectedDialect.Sql.GeneratesCompositeKeyValues)
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
                return;
            }

            await context.SaveChangesAsync(Ct);
        }

        await using var read = Fixture.CreateContext<OwnedLinesContext>(o => new OwnedLinesContext(o));
        read.Database.SetConnectionString(connectionString);
        Assert.Equal(["a", "b"], (await read.Owners.SingleAsync(Ct)).Lines.Select(l => l.Text).Order());
    }

    [Fact]
    public async Task A_database_exists_once_created_and_not_before()
    {
        await using var context = Context(NewDatabase());

        Assert.False(await Creator(context).ExistsAsync(Ct));
        await Creator(context).CreateAsync(Ct);

        Assert.True(await Creator(context).ExistsAsync(Ct));
        Assert.False(await Creator(context).HasTablesAsync(Ct));
    }

    [Fact]
    public void Create_and_delete_work_synchronously_too()
    {
        using var context = Context(NewDatabase());

        Creator(context).Create();
        Assert.True(Creator(context).Exists());
        Creator(context).Delete();

        Assert.False(Creator(context).Exists());
    }

    [Fact]
    public async Task Create_refuses_a_database_that_already_exists()
    {
        await using var context = Context(NewDatabase());
        await Creator(context).CreateAsync(Ct);

        var refused = await Record.ExceptionAsync(() => Creator(context).CreateAsync(Ct));

        Assert.True(refused is DbException or InvalidOperationException, $"Expected the database to refuse: {refused}");
    }

    [Fact]
    public async Task Delete_refuses_a_database_that_does_not_exist()
    {
        await using var context = Context(NewDatabase());

        var refused = await Record.ExceptionAsync(() => Creator(context).DeleteAsync(Ct));

        Assert.True(refused is DbException or InvalidOperationException, $"Expected the database to refuse: {refused}");
    }

    [Fact]
    public async Task Ensure_created_creates_the_database_and_its_tables_once()
    {
        await using (var context = Context(NewDatabase()))
        {
            Assert.True(await context.Database.EnsureCreatedAsync(Ct));
            Assert.False(await context.Database.EnsureCreatedAsync(Ct));

            context.Add(new Blog { Name = "created", CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
            await context.SaveChangesAsync(Ct);
            Assert.True(await Creator(context).HasTablesAsync(Ct));
        }
    }

    [Fact]
    public async Task Ensure_created_adds_tables_to_an_existing_empty_database()
    {
        await using var context = Context(NewDatabase());
        await Creator(context).CreateAsync(Ct);

        Assert.True(await context.Database.EnsureCreatedAsync(Ct));

        Assert.Equal(0, await context.Blogs.CountAsync(Ct));
    }

    [Fact]
    public async Task Ensure_deleted_deletes_and_then_does_nothing()
    {
        await using var context = Context(NewDatabase());
        await context.Database.EnsureCreatedAsync(Ct);

        Assert.True(await context.Database.EnsureDeletedAsync(Ct));
        Assert.False(await context.Database.EnsureDeletedAsync(Ct));

        Assert.False(await Creator(context).ExistsAsync(Ct));
    }

    [Fact]
    public async Task A_deleted_database_can_be_created_again()
    {
        await using var context = Context(NewDatabase());
        await context.Database.EnsureCreatedAsync(Ct);
        context.Add(new Blog { Name = "first", CreatedAt = new DateTime(2026, 1, 2), ExternalId = Guid.NewGuid() });
        await context.SaveChangesAsync(Ct);

        await context.Database.EnsureDeletedAsync(Ct);
        await context.Database.EnsureCreatedAsync(Ct);

        Assert.Equal(0, await context.Blogs.CountAsync(Ct));
    }

    [Fact]
    public async Task Ensure_created_refuses_when_only_the_other_kind_of_seeder_is_configured()
    {
        var connectionString = NewDatabase();

        await using (var asyncOnly = Seeded(connectionString, o => o.UseAsyncSeeding((_, _, _) => Task.CompletedTask)))
        {
            Assert.Throws<InvalidOperationException>(() => asyncOnly.Database.EnsureCreated());
        }

        await using (var syncOnly = Seeded(connectionString, o => o.UseSeeding((_, _) => { })))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => syncOnly.Database.EnsureCreatedAsync(Ct));
        }
    }

    private BlogContext Seeded(string connectionString, Action<DbContextOptionsBuilder<BlogContext>> seeding)
    {
        var builder = new DbContextOptionsBuilder<BlogContext>(Fixture.Options);
        seeding(builder);
        var context = new BlogContext(builder.Options);
        context.Database.SetConnectionString(connectionString);
        return context;
    }

    [Fact]
    public void The_create_script_creates_every_table()
    {
        using var context = Fixture.CreateContext();

        var script = Creator(context).GenerateCreateScript();

        Assert.Contains(Fixture.Quote("Blogs"), script);
        Assert.Contains(Fixture.Quote("Posts"), script);
    }

    /// <summary>
    ///     Two tables whose foreign keys refer to each other: EF creates both, then adds the keys, which some databases take
    ///     only in CREATE TABLE. The keys are enforced afterwards. Behaviour reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Migrations/SqliteMigrationsSqlGenerator.cs
    ///     Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite and DuckDB.
    /// </summary>
    [Fact]
    public async Task Ensure_created_creates_tables_whose_foreign_keys_refer_to_each_other()
    {
        var connectionString = NewDatabase();
        await using (var context = Cyclic(connectionString))
        {
            Assert.True(await context.Database.EnsureCreatedAsync(Ct));
            // Each row refers to the other, so EF needs two saves: the rows, then the reference that closes the cycle.
            var household = new Household { Name = "home" };
            var person = new Person { Name = "head", Household = household };
            context.Add(person);
            await context.SaveChangesAsync(Ct);
            household.Head = person;
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Cyclic(connectionString))
        {
            var household = await context.Set<Household>().Include(h => h.Head).SingleAsync(Ct);
            Assert.Equal("head", household.Head!.Name);

            context.Add(new Person { Name = "stray", HouseholdId = 999_999 });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }
    }

    private CyclicContext Cyclic(string connectionString)
    {
        var context = Fixture.CreateContext<CyclicContext>(o => new CyclicContext(o));
        context.Database.SetConnectionString(connectionString);
        return context;
    }

    private sealed class Household
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? HeadId { get; set; }
        public Person? Head { get; set; }
    }

    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? HouseholdId { get; set; }
        public Household? Household { get; set; }
    }

    private sealed class CyclicContext(DbContextOptions<CyclicContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Household>().HasOne(h => h.Head).WithMany().HasForeignKey(h => h.HeadId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Person>().HasOne(p => p.Household).WithMany().HasForeignKey(p => p.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    /// <summary>
    ///     One table reached from another by two paths of cascading deletes (a diamond): every database creates it, and deleting
    ///     the root deletes its dependents, by the database or, where it allows one cascade path only, by EF. Behaviour
    ///     reference (not copied):
    ///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Conventions/SqlServerOnDeleteConvention.cs
    ///     Runs on: MySQL, PostgreSQL, SQL Server, flatfile, SQLite and DuckDB.
    /// </summary>
    [Fact]
    public async Task Ensure_created_creates_a_table_reached_by_two_cascade_paths_and_deletes_cascade()
    {
        var connectionString = NewDatabase();
        await using (var context = Diamond(connectionString))
        {
            Assert.True(await context.Database.EnsureCreatedAsync(Ct));
            var root = new Root();
            var left = new Left { Root = root };
            var right = new Right { Root = root };
            context.Add(new Leaf { Left = left, Right = right });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Diamond(connectionString))
        {
            var root = await context.Set<Root>().Include(r => r.Lefts).ThenInclude(l => l.Leaves)
                .Include(r => r.Rights).ThenInclude(r => r.Leaves).SingleAsync(Ct);
            context.Remove(root);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Diamond(connectionString))
        {
            Assert.Equal(0, await context.Set<Leaf>().CountAsync(Ct));
            Assert.Equal(0, await context.Set<Left>().CountAsync(Ct) + await context.Set<Right>().CountAsync(Ct));
        }
    }

    private DiamondContext Diamond(string connectionString)
    {
        var context = Fixture.CreateContext<DiamondContext>(o => new DiamondContext(o));
        context.Database.SetConnectionString(connectionString);
        return context;
    }

    private sealed class Root
    {
        public int Id { get; set; }
        public List<Left> Lefts { get; set; } = [];
        public List<Right> Rights { get; set; } = [];
    }

    private sealed class Left
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
        public List<Leaf> Leaves { get; set; } = [];
    }

    private sealed class Right
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root Root { get; set; } = null!;
        public List<Leaf> Leaves { get; set; } = [];
    }

    private sealed class Leaf
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public Left Left { get; set; } = null!;
        public int RightId { get; set; }
        public Right Right { get; set; } = null!;
    }

    // Every relationship is required, so EF's conventions cascade each delete.
    private sealed class DiamondContext(DbContextOptions<DiamondContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Root>();
            modelBuilder.Entity<Left>();
            modelBuilder.Entity<Right>();
            modelBuilder.Entity<Leaf>();
        }
    }
}
