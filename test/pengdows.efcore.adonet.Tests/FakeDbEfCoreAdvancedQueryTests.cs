using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Tests;

/// <summary>Advanced EF query pipelines that remain deterministic when the database rows are scripted by fakeDb.</summary>
public sealed class FakeDbEfCoreAdvancedQueryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Split_include_consumes_multiple_result_sets_and_fixes_up_the_collection()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<BlogContext>(AdoNetDialect.Ansi, options => new BlogContext(options));
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "blog" }]);
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?> { ["Id"] = 2, ["BlogId"] = 1, ["Title"] = "post", ["BlogKey"] = 1 }]);

        var blog = await context.Blogs.AsSplitQuery().Include(item => item.Posts).SingleAsync(Ct);

        Assert.Equal("blog", blog.Name);
        var post = Assert.Single(blog.Posts);
        Assert.Equal((2, "post"), (post.Id, post.Title));
        Assert.Same(blog, post.Blog);
    }

    [Fact]
    public async Task Split_include_fixes_up_many_to_many_skip_navigations_from_fakeDb_result_sets()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<ManyToManyContext>(AdoNetDialect.Ansi, options => new ManyToManyContext(options));
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "group" }]);
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?>
            {
                ["GroupsId"] = 1,
                ["MembersId"] = 2,
                ["Id"] = 2,
                ["Name"] = "member",
                ["Id0"] = 1
            }]);

        var group = await context.Groups.AsSplitQuery().Include(item => item.Members).SingleAsync(Ct);

        var member = Assert.Single(group.Members);
        Assert.Equal((2, "member"), (member.Id, member.Name));
        Assert.Contains(group, member.Groups);
    }

    [Fact]
    public async Task Compiled_async_queries_use_the_fakeDb_reader_pipeline()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<CompiledContext>(AdoNetDialect.Ansi, options => new CompiledContext(options));
        database.Returns(FakeDatabase.Row("Name", "compiled"));

        var query = EF.CompileAsyncQuery((CompiledContext db, int id)
            => db.Items.Where(item => item.Id == id).Select(item => item.Name).Single());

        var name = await query(context, 7);

        Assert.Equal("compiled", name);
        Assert.Contains("SELECT", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task Tph_queries_materialize_the_derived_entity_from_the_discriminator()
    {
        var database = new FakeDatabase();
        await using var context = database.ContextOf<InheritanceContext>(AdoNetDialect.Ansi, options => new InheritanceContext(options));
        database.Returns(new Dictionary<string, object?>
        {
            ["Id"] = 3,
            ["Discriminator"] = "Employee",
            ["Name"] = "employee",
            ["Department"] = "engineering"
        });

        var employee = await context.People.OfType<Employee>().SingleAsync(Ct);

        Assert.Equal((3, "employee", "engineering"), (employee.Id, employee.Name, employee.Department));
        Assert.Contains("Discriminator", Assert.Single(database.Commands));
    }

    [Fact]
    public async Task Lazy_loading_proxies_run_the_navigation_query_through_fakeDb()
    {
        var database = new FakeDatabase();
        var options = new DbContextOptionsBuilder<LazyBlogContext>()
            .UseLazyLoadingProxies()
            .UseAdoNet(database.Connection, contextOwnsConnection: false, o => o.UseDialect(AdoNetDialect.Ansi))
            .AllowManyDialects()
            .Options;
        await using var context = new LazyBlogContext(options);
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "blog" }]);
        database.Connection.EnqueueReaderResult(
            [new Dictionary<string, object?> { ["Id"] = 2, ["BlogId"] = 1, ["Title"] = "post" }]);

        var blog = await context.Blogs.SingleAsync(Ct);
        var post = Assert.Single(blog.Posts);

        Assert.Equal("post", post.Title);
        Assert.Same(blog, post.Blog);
    }

    private sealed class Blog
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<Post> Posts { get; set; } = [];
    }

    private sealed class Post
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Title { get; set; } = "";
        public Blog Blog { get; set; } = null!;
    }

    private sealed class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Post> Posts => Set<Post>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Post>().HasOne(post => post.Blog).WithMany(blog => blog.Posts).HasForeignKey(post => post.BlogId);
    }

    private sealed class CompiledItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class CompiledContext(DbContextOptions<CompiledContext> options) : DbContext(options)
    {
        public DbSet<CompiledItem> Items => Set<CompiledItem>();
    }

    private abstract class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Employee : Person
    {
        public string Department { get; set; } = "";
    }

    private sealed class InheritanceContext(DbContextOptions<InheritanceContext> options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Person>().HasDiscriminator<string>("Discriminator")
                .HasValue<Person>("Person")
                .HasValue<Employee>("Employee");
    }

    public class LazyBlog
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public virtual ICollection<LazyPost> Posts { get; set; } = [];
    }

    public class LazyPost
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Title { get; set; } = "";
        public virtual LazyBlog Blog { get; set; } = null!;
    }

    private sealed class LazyBlogContext(DbContextOptions<LazyBlogContext> options) : DbContext(options)
    {
        public DbSet<LazyBlog> Blogs => Set<LazyBlog>();
        public DbSet<LazyPost> Posts => Set<LazyPost>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<LazyPost>().HasOne(post => post.Blog).WithMany(blog => blog.Posts).HasForeignKey(post => post.BlogId);
    }

    private sealed class ManyToManyContext(DbContextOptions<ManyToManyContext> options) : DbContext(options)
    {
        public DbSet<ManyToManyGroup> Groups => Set<ManyToManyGroup>();
        public DbSet<ManyToManyMember> Members => Set<ManyToManyMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ManyToManyGroup>().HasMany(group => group.Members).WithMany(member => member.Groups);
    }

    private sealed class ManyToManyGroup
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ManyToManyMember> Members { get; set; } = [];
    }

    private sealed class ManyToManyMember
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ManyToManyGroup> Groups { get; set; } = [];
    }
}
