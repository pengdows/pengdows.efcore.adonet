using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     Compares the same EF model and workload through Oracle's EF provider and pengdows' provider.
///     The workload uses 100 blogs and 5,000 posts, exercising aggregates, filtering, ordering, relationship
///     navigation, projections, and updates. It is still a smoke benchmark, not a production capacity test.
/// </summary>
public sealed class MySqlProviderComparisonTests(MySqlFixture fixture, ITestOutputHelper output)
    : IClassFixture<MySqlFixture>
{
    [Fact]
    public async Task Providers_return_the_same_results_for_the_same_workload()
    {
        var oracle = await RunCorrectnessWorkload(
            "comparison_oracle_" + Guid.NewGuid().ToString("N"),
            ProviderKind.Oracle);
        var oursConnector = await RunCorrectnessWorkload(
            "comparison_ours_" + Guid.NewGuid().ToString("N"),
            ProviderKind.OursMySqlConnector);
        var oursMySqlData = await RunCorrectnessWorkload(
            "comparison_ours_data_" + Guid.NewGuid().ToString("N"),
            ProviderKind.OursMySqlData);

        AssertEquivalent(oracle, oursConnector);
        AssertEquivalent(oracle, oursMySqlData);
        output.WriteLine($"Correctness fingerprint: {Fingerprint(oracle)}");
    }

    [Fact]
    public async Task Providers_have_comparable_steady_state_performance()
    {
        const int iterations = 30;
        var oracle = await Measure(iterations, ProviderKind.Oracle);
        var oursConnector = await Measure(iterations, ProviderKind.OursMySqlConnector);
        var oursMySqlData = await Measure(iterations, ProviderKind.OursMySqlData);

        output.WriteLine($"Oracle MySql.EntityFrameworkCore: {oracle.TotalMilliseconds:N0} ms total, "
            + $"{oracle.TotalMilliseconds / iterations:N1} ms/iteration");
        output.WriteLine($"pengdows + MySqlConnector:       {oursConnector.TotalMilliseconds:N0} ms total, "
            + $"{oursConnector.TotalMilliseconds / iterations:N1} ms/iteration, "
            + $"{oursConnector.TotalMilliseconds / oracle.TotalMilliseconds:N2}x Oracle");
        output.WriteLine($"pengdows + MySql.Data:            {oursMySqlData.TotalMilliseconds:N0} ms total, "
            + $"{oursMySqlData.TotalMilliseconds / iterations:N1} ms/iteration, "
            + $"{oursMySqlData.TotalMilliseconds / oracle.TotalMilliseconds:N2}x Oracle");

        // This is a measurement test, not a machine-dependent performance gate.
        Assert.True(oracle.TotalMilliseconds > 0);
        Assert.True(oursConnector.TotalMilliseconds > 0);
        Assert.True(oursMySqlData.TotalMilliseconds > 0);
    }

    private static void AssertEquivalent(ComparisonSnapshot expected, ComparisonSnapshot actual)
    {
        Assert.Equal(expected.Grouped, actual.Grouped);
        Assert.Equal(expected.Filtered, actual.Filtered);
        Assert.Equal(expected.Included, actual.Included);
        Assert.Equal(expected.TopScores, actual.TopScores);
        Assert.Equal(expected.MatchingBlogCount, actual.MatchingBlogCount);
        Assert.Equal(expected.ChangedScore, actual.ChangedScore);
    }

    private static string Fingerprint(ComparisonSnapshot snapshot)
    {
        var text = string.Join('|',
            snapshot.Grouped.Count,
            snapshot.Grouped.Sum(item => item.ScoreTotal),
            snapshot.Filtered.Count,
            snapshot.Filtered.Sum(item => item.Score),
            snapshot.TopScores.Count,
            snapshot.TopScores.Sum(item => item.Score),
            snapshot.MatchingBlogCount,
            snapshot.ChangedScore);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private async Task<ComparisonSnapshot> RunCorrectnessWorkload(string database, ProviderKind provider)
    {
        var connectionString = fixture.ConnectionStringFor(database) + ";AllowPublicKeyRetrieval=true";
        await using var context = CreateContext(connectionString, provider);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        await Seed(context);

        var grouped = await context.Blogs
            .OrderByDescending(blog => blog.Posts.Count)
            .ThenBy(blog => blog.Id)
            .Select(blog => new BlogSummary(blog.Id, blog.Name, blog.Posts.Count, blog.Posts.Sum(post => post.Score)))
            .ToListAsync();

        var filtered = await context.Posts
            .Where(post => post.Title.Contains("needle") && post.Score >= 10)
            .OrderBy(post => post.BlogId)
            .ThenBy(post => post.Id)
            .Select(post => new PostSummary(post.BlogId, post.Title, post.Score))
            .ToListAsync();

        var included = await context.Blogs
            .Include(blog => blog.Posts)
            .OrderBy(blog => blog.Id)
            .Select(blog => new BlogSummary(blog.Id, blog.Name, blog.Posts.Count, blog.Posts.Sum(post => post.Score)))
            .ToListAsync();

        var topScores = await context.Posts
            .Where(post => post.BlogId % 5 == 0)
            .OrderByDescending(post => post.Score)
            .ThenBy(post => post.Id)
            .Take(100)
            .Select(post => new PostSummary(post.BlogId, post.Title, post.Score))
            .ToListAsync();

        var matchingBlogCount = await context.Blogs
            .CountAsync(blog => blog.Posts.Any(post => post.Title.Contains("needle") && post.Score >= 75));

        var changed = await context.Posts.SingleAsync(post => post.Title == "needle-1-3");
        changed.Score += 5;
        await context.SaveChangesAsync();
        var changedScore = await context.Posts
            .Where(post => post.Title == "needle-1-3")
            .Select(post => post.Score)
            .SingleAsync();

        return new ComparisonSnapshot(grouped, filtered, included, topScores, matchingBlogCount, changedScore);
    }

    private async Task<TimeSpan> Measure(int iterations, ProviderKind provider)
    {
        var database = "cmp_perf_" + provider + "_" + Guid.NewGuid().ToString("N")[..8];
        var connectionString = fixture.ConnectionStringFor(database) + ";AllowPublicKeyRetrieval=true";
        await using (var setup = CreateContext(connectionString, provider))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
            await Seed(setup);
            await Query(setup);
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await using var context = CreateContext(connectionString, provider);
            await Query(context);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static async Task Seed(ComparisonContext context)
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

    private static async Task Query(ComparisonContext context)
    {
        _ = await context.Posts
            .Where(post => post.Title.Contains("needle"))
            .GroupBy(post => post.BlogId)
            .OrderBy(group => group.Key)
            .Select(group => new { BlogId = group.Key, Total = group.Sum(post => post.Score) })
            .ToListAsync();

        _ = await context.Blogs
            .Where(blog => blog.Posts.Any(post => post.Title.Contains("needle") && post.Score >= 75))
            .OrderBy(blog => blog.Id)
            .Select(blog => new { blog.Id, blog.Name, Posts = blog.Posts.Count })
            .ToListAsync();

        _ = await context.Posts
            .Where(post => post.BlogId % 5 == 0)
            .OrderByDescending(post => post.Score)
            .ThenBy(post => post.Id)
            .Take(100)
            .Select(post => new { post.BlogId, post.Title, post.Score })
            .ToListAsync();

        _ = await context.Blogs
            .Select(blog => new { blog.Id, Total = blog.Posts.Sum(post => post.Score) })
            .ToListAsync();
    }

    private static ComparisonContext CreateContext(string connectionString, ProviderKind provider)
    {
        var options = new DbContextOptionsBuilder<ComparisonContext>();
        if (provider == ProviderKind.Oracle)
        {
            options.UseMySQL(connectionString);
        }
        else if (provider == ProviderKind.OursMySqlData)
        {
            options.UseAdoNet(
                MySql.Data.MySqlClient.MySqlClientFactory.Instance,
                connectionString,
                MySqlDialect.Instance);
        }
        else
        {
            options.UseAdoNetMySql(connectionString);
        }

        return new ComparisonContext(options.Options);
    }

    private enum ProviderKind
    {
        Oracle,
        OursMySqlConnector,
        OursMySqlData
    }

    private sealed record ComparisonSnapshot(
        IReadOnlyList<BlogSummary> Grouped,
        IReadOnlyList<PostSummary> Filtered,
        IReadOnlyList<BlogSummary> Included,
        IReadOnlyList<PostSummary> TopScores,
        int MatchingBlogCount,
        int ChangedScore);

    private sealed record BlogSummary(int Id, string Name, int PostCount, int ScoreTotal);

    private sealed record PostSummary(int BlogId, string Title, int Score);

    private sealed class ComparisonContext(DbContextOptions<ComparisonContext> options) : DbContext(options)
    {
        public DbSet<ComparisonBlog> Blogs => Set<ComparisonBlog>();
        public DbSet<ComparisonPost> Posts => Set<ComparisonPost>();

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

    private sealed class ComparisonBlog
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<ComparisonPost> Posts { get; } = [];
    }

    private sealed class ComparisonPost
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Title { get; set; } = "";
        public int Score { get; set; }
        public ComparisonBlog Blog { get; set; } = null!;
    }
}
