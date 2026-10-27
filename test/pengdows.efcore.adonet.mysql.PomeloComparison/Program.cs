using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Testcontainers.MySql;

var container = new MySqlBuilder("mysql:8.4").WithUsername("root").Build();
await container.StartAsync();

try
{
    var connectionString = container.GetConnectionString();
    await using (var setup = new ComparisonContext(connectionString))
    {
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.EnsureCreatedAsync();
        await Seed(setup);
    }

    var fingerprint = await Fingerprint(connectionString);
    const int iterations = 30;
    _ = await Query(connectionString);
    var stopwatch = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
    {
        _ = await Query(connectionString);
    }

    stopwatch.Stop();
    Console.WriteLine($"Provider: Pomelo.EntityFrameworkCore.MySql 8 + MySqlConnector");
    Console.WriteLine($"Correctness fingerprint: {fingerprint}");
    Console.WriteLine($"Timing: {stopwatch.Elapsed.TotalMilliseconds:N0} ms total, "
        + $"{stopwatch.Elapsed.TotalMilliseconds / iterations:N1} ms/iteration");
}
finally
{
    await container.DisposeAsync();
}

static async Task Seed(ComparisonContext context)
{
    for (var blogId = 1; blogId <= 100; blogId++)
    {
        var blog = new ComparisonBlog { Name = $"blog-{blogId:000}" };
        for (var postId = 1; postId <= 50; postId++)
        {
            blog.Posts.Add(new ComparisonPost
            {
                Title = postId % 3 == 0 ? $"needle-{blogId}-{postId}" : $"post-{blogId}-{postId}",
                Score = blogId + postId
            });
        }

        context.Blogs.Add(blog);
    }

    await context.SaveChangesAsync();
}

static async Task<string> Fingerprint(string connectionString)
{
    await using var context = new ComparisonContext(connectionString);
    var grouped = await context.Blogs
        .OrderByDescending(blog => blog.Posts.Count)
        .ThenBy(blog => blog.Id)
        .Select(blog => new { blog.Id, blog.Name, Count = blog.Posts.Count, Total = blog.Posts.Sum(post => post.Score) })
        .ToListAsync();
    var filtered = await context.Posts
        .Where(post => post.Title.Contains("needle") && post.Score >= 10)
        .OrderBy(post => post.BlogId)
        .ThenBy(post => post.Id)
        .Select(post => new { post.BlogId, post.Title, post.Score })
        .ToListAsync();
    var top = await context.Posts
        .Where(post => post.BlogId % 5 == 0)
        .OrderByDescending(post => post.Score)
        .ThenBy(post => post.Id)
        .Take(100)
        .Select(post => new { post.BlogId, post.Title, post.Score })
        .ToListAsync();
    var matchingBlogs = await context.Blogs.CountAsync(blog => blog.Posts.Any(post => post.Title.Contains("needle") && post.Score >= 75));
    var changed = await context.Posts.SingleAsync(post => post.Title == "needle-1-3");
    changed.Score += 5;
    await context.SaveChangesAsync();

    var text = string.Join('|',
        grouped.Count,
        grouped.Sum(item => item.Total),
        filtered.Count,
        filtered.Sum(item => item.Score),
        top.Count,
        top.Sum(item => item.Score),
        matchingBlogs,
        changed.Score);
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

static async Task<int> Query(string connectionString)
{
    await using var context = new ComparisonContext(connectionString);
    var a = await context.Posts
        .Where(post => post.Title.Contains("needle"))
        .GroupBy(post => post.BlogId)
        .OrderBy(group => group.Key)
        .Select(group => new { group.Key, Total = group.Sum(post => post.Score) })
        .ToListAsync();
    var b = await context.Blogs
        .Where(blog => blog.Posts.Any(post => post.Title.Contains("needle") && post.Score >= 75))
        .OrderBy(blog => blog.Id)
        .Select(blog => new { blog.Id, blog.Name, Posts = blog.Posts.Count })
        .ToListAsync();
    var c = await context.Posts
        .Where(post => post.BlogId % 5 == 0)
        .OrderByDescending(post => post.Score)
        .ThenBy(post => post.Id)
        .Take(100)
        .Select(post => new { post.BlogId, post.Title, post.Score })
        .ToListAsync();
    var d = await context.Blogs
        .Select(blog => new { blog.Id, Total = blog.Posts.Sum(post => post.Score) })
        .ToListAsync();
    return a.Count + b.Count + c.Count + d.Count;
}

sealed class ComparisonContext(string connectionString) : DbContext
{
    public DbSet<ComparisonBlog> Blogs => Set<ComparisonBlog>();
    public DbSet<ComparisonPost> Posts => Set<ComparisonPost>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComparisonBlog>(entity =>
        {
            entity.ToTable("comparison_blogs");
            entity.HasKey(blog => blog.Id);
            entity.Property(blog => blog.Name).HasMaxLength(64).IsRequired();
            entity.HasMany(blog => blog.Posts).WithOne(post => post.Blog).HasForeignKey(post => post.BlogId);
        });
        modelBuilder.Entity<ComparisonPost>(entity =>
        {
            entity.ToTable("comparison_posts");
            entity.HasKey(post => post.Id);
            entity.Property(post => post.Title).HasMaxLength(128).IsRequired();
        });
    }
}

sealed class ComparisonBlog
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<ComparisonPost> Posts { get; } = [];
}

sealed class ComparisonPost
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Title { get; set; } = "";
    public int Score { get; set; }
    public ComparisonBlog Blog { get; set; } = null!;
}
