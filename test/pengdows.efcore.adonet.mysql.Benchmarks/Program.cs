using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using pengdows.efcore.adonet;
using pengdows.efcore.adonet.mysql;
using Testcontainers.MySql;

BenchmarkRunner.Run<MySqlProviderBenchmark>();

/// <summary>
///     Reproducible provider comparison for the same EF 10 workload. BenchmarkDotNet supplies process isolation,
///     warmup, repeated measurements and variance; the Pomelo EF 8 comparison remains a separate, version-labelled
///     console lane in <c>mysql.PomeloComparison</c>.
/// </summary>
[MemoryDiagnoser]
public class MySqlProviderBenchmark
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();
    private string _connectionString = "";

    [GlobalSetup]
    public async Task Setup()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString() + ";AllowPublicKeyRetrieval=true;UseAffectedRows=false";
        foreach (var provider in Enum.GetValues<Provider>())
        {
            await using var context = CreateContext(provider);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            await Seed(context);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _container.DisposeAsync();

    [Benchmark(Baseline = true)]
    public Task OracleEf10() => Query(CreateContext(Provider.Oracle));

    [Benchmark]
    public Task AdoNetMySqlConnector() => Query(CreateContext(Provider.MySqlConnector));

    [Benchmark]
    public Task AdoNetMySqlData() => Query(CreateContext(Provider.MySqlData));

    private ComparisonContext CreateContext(Provider provider)
    {
        var options = new DbContextOptionsBuilder<ComparisonContext>();
        switch (provider)
        {
            case Provider.Oracle:
                options.UseMySQL(_connectionString);
                break;
            case Provider.MySqlData:
                options.UseAdoNet(MySql.Data.MySqlClient.MySqlClientFactory.Instance, _connectionString, MySqlDialect.Instance);
                break;
            default:
                options.UseAdoNetMySql(_connectionString);
                break;
        }

        return new ComparisonContext(options.Options);
    }

    private static async Task Seed(ComparisonContext context)
    {
        for (var blogId = 1; blogId <= 100; blogId++)
        {
            var blog = new Blog { Name = $"blog-{blogId:000}" };
            for (var postId = 1; postId <= 50; postId++)
            {
                blog.Posts.Add(new Post { Title = postId % 3 == 0 ? $"needle-{blogId}-{postId}" : $"post-{blogId}-{postId}", Score = blogId + postId });
            }
            context.Blogs.Add(blog);
        }
        await context.SaveChangesAsync();
    }

    private static async Task Query(ComparisonContext context)
    {
        await using (context)
        {
            _ = await context.Posts.Where(p => p.Title.Contains("needle")).GroupBy(p => p.BlogId).Select(g => new { g.Key, Total = g.Sum(p => p.Score) }).ToListAsync();
            _ = await context.Blogs.Where(b => b.Posts.Any(p => p.Title.Contains("needle") && p.Score >= 75)).Select(b => new { b.Id, b.Name, Posts = b.Posts.Count }).ToListAsync();
            _ = await context.Posts.Where(p => p.BlogId % 5 == 0).OrderByDescending(p => p.Score).Take(100).Select(p => new { p.BlogId, p.Title, p.Score }).ToListAsync();
            _ = await context.Blogs.Select(b => new { b.Id, Total = b.Posts.Sum(p => p.Score) }).ToListAsync();
        }
    }

    private enum Provider { Oracle, MySqlConnector, MySqlData }

    private sealed class ComparisonContext(DbContextOptions<ComparisonContext> options) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Post> Posts => Set<Post>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>(e => { e.ToTable("comparison_blogs"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(64).IsRequired(); e.HasMany(x => x.Posts).WithOne(x => x.Blog).HasForeignKey(x => x.BlogId); });
            modelBuilder.Entity<Post>(e => { e.ToTable("comparison_posts"); e.HasKey(x => x.Id); e.Property(x => x.Title).HasMaxLength(128).IsRequired(); });
        }
    }

    private sealed class Blog { public int Id { get; set; } public string Name { get; set; } = ""; public List<Post> Posts { get; } = []; }
    private sealed class Post { public int Id { get; set; } public int BlogId { get; set; } public string Title { get; set; } = ""; public int Score { get; set; } public Blog Blog { get; set; } = null!; }
}
