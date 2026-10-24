using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     One suite, written only against EF Core and ADO.NET abstractions, run unchanged against every provider.
/// </summary>
public abstract class ProviderConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (Fixture.KnownEngineGaps.TryGetValue(TestContext.Current.Test!.TestCase.TestMethodName ?? "", out var reason))
        {
            Assert.Skip(reason);
        }

        await Fixture.ResetAsync();
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    [Fact]
    public void Dialect_is_detected_from_the_provider_factory()
    {
        using var context = Fixture.CreateContext();

        Assert.Same(Fixture.ExpectedDialect, context.GetService<IDbContextOptions>().FindExtension<AdoNetOptionsExtension>()!.Dialect);
    }

    [Fact]
    public async Task Insert_reads_back_generated_keys()
    {
        await using var context = Fixture.CreateContext();
        var blog = NewBlog("one");
        blog.Posts.Add(new Post { Title = "first" });
        blog.Posts.Add(new Post { Title = "second" });

        context.Add(blog);
        await context.SaveChangesAsync(Ct);

        Assert.True(blog.Id > 0);
        Assert.All(blog.Posts, p => Assert.Equal(blog.Id, p.BlogId));
        Assert.Equal(2, blog.Posts.Select(p => p.Id).Distinct().Count());
        Assert.DoesNotContain(0, blog.Posts.Select(p => p.Id));
    }

    [Fact]
    public async Task Round_trips_every_mapped_type()
    {
        var expected = NewBlog("types", rating: 12.34m, active: true);
        await using (var context = Fixture.CreateContext())
        {
            context.Add(expected);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var actual = await context.Blogs.SingleAsync(b => b.Id == expected.Id, Ct);

            Assert.Equal(expected.Name, actual.Name);
            Assert.True(actual.IsActive);
            Assert.Equal(12.34m, actual.Rating);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.ExternalId, actual.ExternalId);
        }
    }

    [Fact]
    public async Task Query_filters_orders_and_pages()
    {
        await SeedAsync(10);
        await using var context = Fixture.CreateContext();
        var minRating = 3m;

        var page = await context.Blogs
            .Where(b => b.Rating >= minRating && b.IsActive)
            .OrderByDescending(b => b.Rating)
            .ThenBy(b => b.Name)
            .Skip(1)
            .Take(2)
            .Select(b => b.Name)
            .ToListAsync(Ct);

        // Active blogs are the even ones: ratings 8, 6, 4 (>= 3); skip 1, take 2.
        Assert.Equal(["blog6", "blog4"], page);
    }

    [Fact]
    public async Task Same_query_with_different_paging_values()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        foreach (var (skip, take) in new[] { (0, 1), (1, 3), (4, 10) })
        {
            var names = await context.Blogs.OrderBy(b => b.Rating).Skip(skip).Take(take).Select(b => b.Name).ToListAsync(Ct);

            Assert.Equal(Enumerable.Range(skip, Math.Min(take, 5 - skip)).Select(i => $"blog{i}"), names);
        }
    }

    [Fact]
    public async Task Query_offset_without_limit()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        var names = await context.Blogs.OrderBy(b => b.Rating).Skip(3).Select(b => b.Name).ToListAsync(Ct);

        Assert.Equal(["blog3", "blog4"], names);
    }

    [Fact]
    public async Task Query_aggregates_and_boolean_constants()
    {
        await SeedAsync(5);
        await using var context = Fixture.CreateContext();

        Assert.Equal(5, await context.Blogs.CountAsync(Ct));
        Assert.Equal(5L, await context.Blogs.LongCountAsync(Ct));
        Assert.Equal(3, await context.Blogs.CountAsync(b => b.IsActive == true, Ct));
        Assert.Equal(2, await context.Blogs.CountAsync(b => !b.IsActive, Ct));
        Assert.True(await context.Blogs.AnyAsync(b => b.Name == "blog4", Ct));
        Assert.Equal(4m, await context.Blogs.MaxAsync(b => b.Rating, Ct));
    }

    [Fact]
    public async Task Query_includes_collection_navigation()
    {
        await using (var context = Fixture.CreateContext())
        {
            var blog = NewBlog("with-posts");
            blog.Posts.AddRange([new Post { Title = "a" }, new Post { Title = "b" }]);
            context.Add(blog);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var blog = await context.Blogs.Include(b => b.Posts).SingleAsync(Ct);

            Assert.Equal(["a", "b"], blog.Posts.Select(p => p.Title).Order());
        }
    }

    [Fact]
    public async Task Update_and_delete()
    {
        await SeedAsync(3);

        await using (var context = Fixture.CreateContext())
        {
            var blogs = await context.Blogs.OrderBy(b => b.Rating).ToListAsync(Ct);
            blogs[0].Rating = 99m;
            context.Remove(blogs[1]);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            var remaining = await context.Blogs.OrderBy(b => b.Name).Select(b => new { b.Name, b.Rating }).ToListAsync(Ct);

            Assert.Equal([new { Name = "blog0", Rating = 99m }, new { Name = "blog2", Rating = 2m }], remaining);
        }
    }

    [Fact]
    public async Task Concurrency_conflict_is_detected()
    {
        await SeedAsync(1);
        await using var context = Fixture.CreateContext();
        var blog = await context.Blogs.SingleAsync(Ct);

        await Fixture.ExecuteAsync($"UPDATE \"Blogs\" SET \"Name\" = 'changed elsewhere' WHERE \"Id\" = {blog.Id}");

        blog.Name = "mine";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_no_rows()
    {
        await using (var context = Fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Add(NewBlog("rolled-back"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(0, await context.Blogs.CountAsync(Ct));
        }
    }

    [Fact]
    public async Task Connection_is_closed_between_operations()
    {
        await SeedAsync(1);
        await using var context = Fixture.CreateContext();

        await context.Blogs.ToListAsync(Ct);

        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Has_tables()
    {
        await using var context = Fixture.CreateContext();

        Assert.True(await context.Database.CanConnectAsync(Ct));
        Assert.True(context.GetService<IRelationalDatabaseCreator>().HasTables());
    }

    [Fact]
    public async Task Savepoints_follow_the_ado_net_transaction()
    {
        await using (var context = Fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);

            Assert.Equal(transaction.GetDbTransaction().SupportsSavepoints, transaction.SupportsSavepoints);
            if (!transaction.SupportsSavepoints)
            {
                return;
            }

            context.Add(NewBlog("kept"));
            await context.SaveChangesAsync(Ct);
            await transaction.CreateSavepointAsync("before_discard", Ct);
            context.Add(NewBlog("discarded"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackToSavepointAsync("before_discard", Ct);
            await transaction.CommitAsync(Ct);
        }

        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(["kept"], await context.Blogs.Select(b => b.Name).ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task Insert_uses_the_dialects_generated_value_strategy()
    {
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BlogContext>(Fixture.Options)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted])
            .Options;

        await using var context = new BlogContext(options);
        context.Add(NewBlog("logged"));
        await context.SaveChangesAsync(Ct);

        var insert = Assert.Single(commands, c => c.Contains("INSERT INTO"));
        switch (Fixture.ExpectedDialect.GeneratedValues)
        {
            case GeneratedValueRetrieval.Returning:
                Assert.Contains("RETURNING", insert);
                break;
            case GeneratedValueRetrieval.SelectAfter:
                Assert.DoesNotContain("RETURNING", insert);
                Assert.Contains(Fixture.ExpectedDialect.IdentityFunction!, insert);
                break;
            case GeneratedValueRetrieval.RecordsAffected:
                Assert.DoesNotContain("RETURNING", insert);
                Assert.Contains(commands, c => c.Contains("NEXT VALUE FOR"));
                break;
        }
    }

    protected async Task SeedAsync(int count)
    {
        await using var context = Fixture.CreateContext();
        for (var i = 0; i < count; i++)
        {
            context.Add(NewBlog($"blog{i}", rating: i, active: i % 2 == 0));
        }

        await context.SaveChangesAsync(Ct);
    }

    protected static Blog NewBlog(string name, decimal rating = 1m, bool active = true)
        => new()
        {
            Name = name,
            Rating = rating,
            IsActive = active,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            ExternalId = Guid.NewGuid()
        };
}

public class SqliteConformanceTests(SqliteFixture fixture) : ProviderConformanceTests<SqliteFixture>(fixture);

public class LegacySqliteConformanceTests(LegacySqliteFixture fixture) : ProviderConformanceTests<LegacySqliteFixture>(fixture);

public class DuckDbConformanceTests(DuckDbFixture fixture) : ProviderConformanceTests<DuckDbFixture>(fixture);

public class FlatFileConformanceTests(FlatFileFixture fixture) : ProviderConformanceTests<FlatFileFixture>(fixture);
